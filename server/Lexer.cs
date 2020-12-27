using System;
using System.IO;
using System.Text;

namespace md_svr
{
// シングルスレッドで処理をするため、static クラスで良いことに留意
static unsafe class Lexer
{
// 暫定的に 100 kbytes を用意している（これを超えることはないだろう、、、）
const int EN_MD_buf_size = 100 * 1024;
// ms_file_buf は utf-16le と想定する
static byte[] ms_MD_buf = new byte[EN_MD_buf_size];

static Write_WS_Buffer ms_write_WS_buf;

// ------------------------------------------------------------------------------------
// エラーがあった場合、例外で返される。このとき dst_wrt_WS_buf には、エラーの発生コードと、ID.End が書き込まれる。
// file_path は、"md_root/..." の形のもの
public static void LexFile(Write_WS_Buffer dst_wrt_WS_buf, string file_path)
{
	ms_write_WS_buf = dst_wrt_WS_buf;

	ReadFile_to_MD_buf(file_path);
	// この時点で、MD_buf の最後は \n\0 で終わっている。

	// まず、Lexed_MD をエラー状態で書き込んでおく（エラーがあったときは、例外で外に飛んでいくため）
	ms_write_WS_buf.Wrt_ID_param(ID.Lexed_MD, (byte)Param.Failed);

	// ---------------------------------------------------------
	// １行ごとに字句解析を実行する
	fixed (byte* pstr_MD_top = ms_MD_buf)
	{
		char* psrc = (char*)(pstr_MD_top + 2);  // +2 は、utf-16le の BOM

		while (true)
		{
			// Consume_Line() は、次の行頭を返してくる
			psrc = Consume_Line(psrc);
			// 行頭が \0 であるとき、ファイル読み取りが終了したと判断する
			// エラーの場合は、例外処理で返される
			if (*psrc == 0) { break; }
		}
	}

	// Write_WS_Buffer で THROW_ERR() がコールされた場合、ID_End が既に書き込まれている
	// ここに来た場合、Lexing に成功している
	ms_write_WS_buf.Wrt_ID_param_At(0, ID.Lexed_MD, (byte)Param.Succeeded);
	ms_write_WS_buf.Wrt_ID_End();
}


// ------------------------------------------------------------------------------------
// 戻り値 : int は ファイルの ui16 での文字数（エラー時は -1）
static void ReadFile_to_MD_buf(string file_path)
{
	FileInfo file_info = new FileInfo(file_path);
	long len_file = file_info.Length;
	if (len_file > EN_MD_buf_size - 4)  // -4 は最後に \n\0 を付加するため
	{ throw new Exception($"!!! Lexer 対象のファイルサイズが大きすぎます。ファイルサイズ: {file_info.Length}"); }

	using (FileStream fs = file_info.OpenRead())
	{
		fs.Read(ms_MD_buf, 0, (int)len_file);
	}

	// utf16(le) であるかの確認
	if (ms_MD_buf[0] != 0xFF || ms_MD_buf[1] != 0xFE)
	{ throw new Exception($"!!! 指定されたファイルは utf-16le ではありませんでした。"); }

	// ファイルの最後に \n\0 を書き込んでおく
	// これがエンドマークとなるため、MD ファイルのサイズを記録しておく必要がなくなっている
	ms_MD_buf[len_file] = (byte)Chr.LF;
	ms_MD_buf[len_file + 1] = 0;
	ms_MD_buf[len_file + 2] = 0;
	ms_MD_buf[len_file + 3] = 0;
}


// ------------------------------------------------------------------------------------
static bool msb_next_is_Div = true;  // ファイル先頭は、必ず Div ブロックとなる
static bool msb_Dtct_CodeBlk_Mark = false;

[Flags] enum CodeBlk
{
	none = 0,
	inside = 0b01,
	head = 0b10
}
static CodeBlk msf_CodeBlk = CodeBlk.none;

// psrc : 行頭
// 戻り値 : 次の行頭
static char* Consume_Line(char* psrc)
{
	// -----------------------------------------------------
	// 行頭チェック（#, ```, >）
	switch (*psrc)
	{
	case '#':
		// コードブロックの場合、先頭の # はそのまま表示する
		if (msf_CodeBlk.HasFlag(CodeBlk.inside) == true) { break; }

		// 必ず Head 行として扱う
		return Consume_Head_Line(psrc);

	case '`':
		psrc = Consume_CodeBlk_Mark(psrc);
		if (msb_Dtct_CodeBlk_Mark == true)
		{
			msb_Dtct_CodeBlk_Mark = false;
			return psrc;
		}
		break;
	}

	// code ブロックであった場合の処理
	if (msf_CodeBlk.HasFlag(CodeBlk.inside) == true)
	{
		// CodeBlk の中では、前回の行末での改行処理を行っていないため、ここで改行処理を行う
		if (msf_CodeBlk.HasFlag(CodeBlk.head) == true)
		{ msf_CodeBlk = CodeBlk.inside; }
		else
		{ ms_write_WS_buf.Wrt_ID(ID.BR); }  // head でない場合、改行処理をする

		return ms_write_WS_buf.Cosume_CodeLine(psrc);
	}

	// 先頭が空行で会った場合、空行が何行あったとしても Div が一度しか生成されないようにする
	if (*psrc == Chr.CR) { msb_next_is_Div = true;  return psrc + 2; }
	if (*psrc == Chr.LF) { msb_next_is_Div = true;  return psrc + 1; }

	// 何らかの表示文字列が現れた場合の処理
	if (msb_next_is_Div == true)
	{
		ms_write_WS_buf.Wrt_ID(ID.Div);
		msb_next_is_Div = false;
	}

	psrc = ms_write_WS_buf.Consume_NormalLine(psrc);
	// psrc は、CR or LF のところを指してリターンしてくるはず

	// 行末処理。改行が必要かどうかの判断を行う
	if (*(psrc - 1) == Chr.SP && *(psrc - 2) == Chr.SP)
	{
		ms_write_WS_buf.Wrt_ID(ID.BR);
	}

	if (*psrc == Chr.CR)
	{ return psrc + 2; }
	else
	{ return psrc + 1; }
}

// ------------------------------------------------------------------------------------
static char* Consume_Head_Line(char* psrc)
{
	char* psrc_AtBgn = psrc;

	byte cnt = 1;
	while (*++psrc == '#') { cnt++; }
	if (cnt > 6) { cnt = 6; }

	// 半角スペースが無い場合、エラーとしておく（GitHub との整合性）
	if (*psrc++ != Chr.SP)
	{
		ms_write_WS_buf.THROW_ERR(--psrc, "# の後にスペースがありませんでした。");
		return null;
	}

	ms_write_WS_buf.Wrt_ID_param(ID.Div_Head, cnt);
	msb_next_is_Div = true;

	psrc = ms_write_WS_buf.Consume_NormalLine(psrc);
	if (*psrc == Chr.CR)
	{ return psrc + 2; }
	else
	{ return psrc + 1; }
}

// ------------------------------------------------------------------------------------
static char* Consume_CodeBlk_Mark(char* psrc)
{
	if (*(psrc + 1) != '`') { return psrc; }  // この場合は、inline code

	if (*(psrc + 2) != '`')
	{
		ms_write_WS_buf.THROW_ERR(psrc, "``○ の形の文字列を検出しました。");
		return null;
	}

	// psrc から「```」を検出した場合の処理
	switch (*(psrc + 3))
	{
	case Chr.CR:
		psrc++;
		goto case Chr.LF;

	case Chr.LF:
		msb_Dtct_CodeBlk_Mark = true;

		if (msf_CodeBlk.HasFlag(CodeBlk.inside) == false)
		{
			// CodeBlk に入る
			ms_write_WS_buf.Wrt_ID(ID.Div_Code);
			msf_CodeBlk = CodeBlk.inside | CodeBlk.head;
			msb_next_is_Div = false;
		}
		else
		{
			// CodeBlk から出る
			msf_CodeBlk = CodeBlk.none;
			msb_next_is_Div = true;
		}
		return psrc + 4;
	}

	// ここに来たらエラーとして処理をする
	ms_write_WS_buf.THROW_ERR(psrc, "``` の後に改行がありませんでした。");
	return null;  // THROW_ERR で例外が投げられるため、実際にここに来ることはない
}

// ------------------------------------------------------------------------------------

}  // static unsafe class Lexer
}  // namespace md_svr
