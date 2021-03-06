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
	ReadFile_to_MD_buf(file_path);
	// この時点で、MD_buf の最後は \n\0 で終わっている。

	// ---------------------------------------------------------
	// 字句解析用のフラグを初期化
	msb_next_is_Div = true;  // ファイル先頭は、必ず Div ブロックとなる
	msb_Dtct_CodeBlk_Mark = false;

	msb_is_in_CodeBlk = false;
	msb_is_in_QuoteBlk = false;

	// まず、Lexed_MD をエラー状態で書き込んでおく（エラーがあったときは、例外で外に飛んでいくため）
	ms_write_WS_buf = dst_wrt_WS_buf;
	ms_write_WS_buf.Wrt_ID_param(ID.Lexed_MD, Param.EN_Failed);

	ms_write_WS_buf.Clear_Txt_flags();

	// ---------------------------------------------------------
	// １行ごとに字句解析を実行する
	fixed (byte* pstr_MD_top = ms_MD_buf)
	{
		ushort* psrc = (ushort*)(pstr_MD_top + 2);  // +2 は、utf-16le の BOM

		while (true)
		{
			// Consume_Line() は、次の行頭を返してくる
			psrc = Consume_Line(psrc);
			// 行頭が \0 であるとき、ファイル読み取りが終了したと判断する
			// エラーの場合は、例外処理で返される
			if (*psrc == 0) { break; }
		}
	}

	MainForm.StdOut($"変換後のサイズ : {ms_write_WS_buf.Get_idx_byte_cur().ToString("N0")}\r\n");

	// Write_WS_Buffer で THROW_ERR() がコールされた場合、ID_End が既に書き込まれている
	// ここに来た場合、Lexing に成功している
	ms_write_WS_buf.Wrt_ID_param_At(0, ID.Lexed_MD, Param.EN_Succeeded);
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

	MainForm.StdOut($"ファイルサイズ : {len_file.ToString("N0")}\r\n");

	// ファイルの最後に \n\0 を書き込んでおく
	// これがエンドマークとなるため、MD ファイルのサイズを記録しておく必要がなくなっている
	ms_MD_buf[len_file] = (byte)Chr.LF;
	ms_MD_buf[len_file + 1] = 0;
	ms_MD_buf[len_file + 2] = 0;
	ms_MD_buf[len_file + 3] = 0;
}


// ------------------------------------------------------------------------------------
static bool msb_next_is_Div;
static bool msb_Dtct_CodeBlk_Mark;

static bool msb_is_in_CodeBlk;
static bool msb_is_in_QuoteBlk;

// psrc : 行頭
// 戻り値 : 次の行頭
static ushort* Consume_Line(ushort* psrc)
{
	// -----------------------------------------------------
	// 行頭チェック（#, ```, >）
	ushort chr_linetop = *psrc;

	// Quote は特殊な処理となる
	if (chr_linetop == '>')
	{
		if (msb_is_in_QuoteBlk == false)
		{
			// QuoteBlk に入る
			ms_write_WS_buf.Wrt_ID(ID.Div_Quote);
			msb_is_in_QuoteBlk = true;

			msb_next_is_Div = true;
		}

		chr_linetop = *++psrc;
		if (chr_linetop == Chr.SP) { chr_linetop = *++psrc; }
	}
	else
	{
		// QuoteBlk 解除確認
		if (msb_is_in_QuoteBlk == true)
		{
			ms_write_WS_buf.Wrt_ID(ID.Div_Quote);
			msb_is_in_QuoteBlk = false;

			msb_next_is_Div = true;
		}
	}

	switch (chr_linetop)
	{	
	case '`':
		psrc = Consume_CodeBlk_Mark(psrc);  // CodeBlk_Mark のみを consume する
		if (msb_Dtct_CodeBlk_Mark == true)
		{
			msb_Dtct_CodeBlk_Mark = false;
			return psrc;
		}
		break;

	case '#':
		if (msb_is_in_CodeBlk == true) { break; }

		// 必ず Head 行として扱う（Head でない場合、例外がスローされるようにした）
		return Consume_Head_Line(psrc);

	case '*':
		if (msb_is_in_CodeBlk == true) { break; }

		if (*(psrc + 1) != Chr.SP) { break; }
		// bullet 処理
		ms_write_WS_buf.Wrt_ID(ID.Div_Bullet);
		msb_next_is_Div = false;
		psrc += 2;
		break;

	case '-':
		if (*(uint*)(psrc + 1) == 0x002d_002d)  // 水平線
		{
			ms_write_WS_buf.Wrt_ID(ID.HLine);
			msb_next_is_Div = true;

			// この場合は行末まで読み飛ばすことにした
			psrc += 3;
			while (true)
			{
				ushort chr = *psrc++;
				if (chr == Chr.CR) { return psrc + 1; }
				if (chr == Chr.LF) { return psrc; }
			}
		}
		break;
	}
	// -----------------------------------------------------

	// code ブロックであった場合の処理
	if (msb_is_in_CodeBlk == true)
	{ return ms_write_WS_buf.Cosume_CodeLine(psrc); }

	// 先頭が空行であった場合、空行が何行あったとしても Div が一度しか生成されないようにする
	if (chr_linetop == Chr.CR) { msb_next_is_Div = true;  return psrc + 2; }
	if (chr_linetop == Chr.LF) { msb_next_is_Div = true;  return psrc + 1; }

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
static ushort* Consume_Head_Line(ushort* psrc)
{
	byte cnt = 1;
	while (*++psrc == '#') { cnt++; }
	if (cnt > 6) { cnt = 6; }

	// 半角スペースが無い場合、エラーとしておく（GitHub との整合性）
	if (*psrc++ != Chr.SP)
	{ ms_write_WS_buf.THROW_ERR(--psrc, "# の後にスペースがありませんでした。"); }

	ms_write_WS_buf.Wrt_ID_param(ID.Div_Head, cnt);
	msb_next_is_Div = true;

	psrc = ms_write_WS_buf.Consume_NormalLine(psrc);
	if (*psrc == Chr.CR)
	{ return psrc + 2; }
	else
	{ return psrc + 1; }
}

// ------------------------------------------------------------------------------------
// CodeBlk_Mark のみを consume する
static ushort* Consume_CodeBlk_Mark(ushort* psrc)
{
	if (*(psrc + 1) != '`') { return psrc; }  // この場合は、inline code

	if (*(psrc + 2) != '`')
	{ ms_write_WS_buf.THROW_ERR(psrc, "``○ の形の文字列を検出しました。"); }

	// psrc から「```」を検出した場合の処理
	switch (*(psrc + 3))
	{
	case Chr.CR:
		psrc++;
		goto case Chr.LF;

	case Chr.LF:
		msb_Dtct_CodeBlk_Mark = true;

		if (msb_is_in_CodeBlk == false)
		{
			// CodeBlk に入る
			ms_write_WS_buf.Wrt_ID(ID.Div_Code);
			msb_is_in_CodeBlk = true;
			msb_next_is_Div = false;
		}
		else
		{
			// CodeBlk から出る
//			ms_write_WS_buf.Wrt_ID(ID.Div_Code);
			msb_is_in_CodeBlk = false;
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
