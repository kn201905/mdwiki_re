using System;
using System.Text;

namespace md_svr
{

/////////////////////////////////////////////////////////////////////////////////////
// Read_WS_Buf

class Read_WS_Buf
{
public static UnicodeEncoding ms_utf16_encoding;

byte[] m_ary_buf;

int m_idx_byte = 0;
int m_rem_ui16 = 0;  // コンストラクト時には、読み取りバッファは 0 であるはず

byte m_param_cur = 0;
string m_text_cur = null;
int m_num_int_cur = 0;

// ------------------------------------------------------------------------------------
internal Read_WS_Buf(byte[] buf)
{
	m_ary_buf = buf;
}

// ------------------------------------------------------------------------------------
// 読み取りバッファが空になっている場合は、0 が返される
// Read_ID() は、client から送られてきたデータを読み取るためだけにあることに注意
// text セクションのコンパクションはされていない
public ID Read_ID() 
{
	if (m_rem_ui16 == 0) { return 0; }

	ID id_ret = (ID)m_ary_buf[m_idx_byte];
	m_param_cur = m_ary_buf[m_idx_byte + 1];

	m_idx_byte += 2;
	m_rem_ui16--;

	m_text_cur = null;  // エラー顕在化のために、m_text_cur だけはクリアしておくことにした
	switch (id_ret)
	{
		case ID.Text: {
			if (m_rem_ui16 == 0)
			{ throw new Exception("Read_WS_Buf.Read_ID() : m_rem_ui16 == 0"); }
		
			int len_txt = m_ary_buf[m_idx_byte] + (m_ary_buf[m_idx_byte + 1] << 8);
			if (m_rem_ui16 <= len_txt)
			{ throw new Exception("Read_WS_Buf.Read_ID() : len_txt >= m_rem_ui16"); }

			// 第２引数、第３引数ともに、バイト数で表していることに注意
			m_text_cur = ms_utf16_encoding.GetString(m_ary_buf, m_idx_byte + 2, len_txt << 1);

			m_idx_byte += 2 + (len_txt << 1);
			m_rem_ui16 -= len_txt + 1;
		} break;

		case ID.Num_int:
			unsafe
			{
				fixed (byte* ary_buf_top = m_ary_buf)
				{
					m_num_int_cur = *(int*)(ary_buf_top + m_idx_byte);
				}
			}
			m_idx_byte += 4;
			m_rem_ui16 -= 2;
			break;
	}

	return id_ret;
}

// ------------------------------------------------------------------------------------
// ファイル名と、SEC_Updated の両方を取得する
// ファイルパスを取得するために、m_ary_buf の後方 2048 bytes 地点を利用する（疲れてきたからかなり雑、、、）
// string は「./ ... / ... .md」の形式
public unsafe (string, int, int) Read_Req_MD()
{
	fixed(byte* ary_buf_top = m_ary_buf)
	{
		char* psrc = (char*)(ary_buf_top + m_idx_byte);
		char* psrc_top = psrc;
		if (*psrc != (char)ID.Text)
		{ throw new Exception("Read_Req_MD() : expected -> path_dir"); }

		char* pdst = psrc + 1024;  // ここの値を変更した場合、ms_utf16_encoding の方も変更すること

		// dir 情報の転送
		int len_md_path = *(psrc + 1);
		psrc += 2;
		for (int i = len_md_path; i > 0; --i)
		{ *pdst++ = *psrc++; }

		if (*psrc != (char)ID.Text)
		{ throw new Exception("Read_Req_MD() : expected -> file_name"); }

		// ファイル名の転送
		ushort len_file_name = *(psrc + 1);
		len_md_path += len_file_name + 3;  // +3 :「.md」

		psrc += 2;
		for (ushort i = len_file_name; i > 0; --i)
		{ *pdst++ = *psrc++; }

		// SEC_Updated の設定
		if (*psrc != (char)ID.Num_int)
		{ throw new Exception("Read_Req_MD() : expected -> SEC_Updated"); }

		// SEC_Updated の取り出し
		int SEC_Updated = *(int*)(psrc + 1);

		// offset byte の算出（SEC_Update の ID の位置）
		int offset_byte = (int)(((byte*)psrc) - ((byte*)ary_buf_top));

		// .md の付加
		*(ulong*)pdst = 0x0064_006d_002e;

		// 第２引数、第３引数ともに、バイト数で表していることに注意
		m_text_cur = ms_utf16_encoding.GetString(m_ary_buf, m_idx_byte + 2048, len_md_path << 1);

		return (m_text_cur, SEC_Updated, offset_byte);
	}
}

// ------------------------------------------------------------------------------------
public byte Get_param_cur() => m_param_cur;
public string Get_text_cur() => m_text_cur;
public int Get_Num_int() => m_num_int_cur;

// WebSocket で読み込まれたバイト数を設定することを考えている
public void Renew(int len_bytes)
{
	if ((len_bytes & 1) != 0)
	{ throw new Exception("Read_WS_Buf.Renew() : len_bytes が奇数です。"); }

	m_idx_byte = 0;
	m_rem_ui16 = len_bytes >> 1;
}
}  // Read_WS_Buf


/////////////////////////////////////////////////////////////////////////////////////
// Write_WS_Buffer

public class Write_WS_Buffer
{
// メンバ変数は３つだけ
byte[] m_ary_buf;

int m_idx_byte;
int m_rem_ui16;

const int EN_Margin_buf = 10;  // TAB、テキストセクションの生成などのマージン

// ------------------------------------------------------------------------------------
// コンストラクタ
public Write_WS_Buffer(byte[] buf)
{
	m_ary_buf = buf;

	m_idx_byte = 0;
	m_rem_ui16 = (buf.Length >> 1) - 2;  // エラー報告と、ID.End を必ず書き込めるようにするため
}

// ------------------------------------------------------------------------------------
public byte[] Get_ary_buf() => m_ary_buf;
public int Get_idx_byte_cur() => m_idx_byte;

// ------------------------------------------------------------------------------------
public void Flush()
{
	m_idx_byte = 0;
	m_rem_ui16 = (m_ary_buf.Length >> 1) - 2;  // エラー報告と、ID.End を必ず書き込めるようにするため
}

// ------------------------------------------------------------------------------------
public void Set_idx_byte(int idx_byte)
{
	if ((idx_byte & 1) == 1)
	{
		// idx_byte が奇数である場合はエラーとする
		throw new Exception("Write_WS_Buffer.Set_idx_byte() : idx_byte の値が奇数となっています。");
	}
	m_idx_byte = idx_byte;
	m_rem_ui16 = (m_ary_buf.Length - idx_byte) >> 1;
}

// ------------------------------------------------------------------------------------
public System.Threading.Tasks.Task SendAsync(
		System.Net.WebSockets.WebSocket WS, System.Threading.CancellationTokenSource cts_shutdown)
{
	return WS.SendAsync(
			new ArraySegment<byte>(m_ary_buf, 0, m_idx_byte)
			, System.Net.WebSockets.WebSocketMessageType.Binary, true  // endOfMessage
			, cts_shutdown.Token );
}

// ------------------------------------------------------------------------------------
public void Wrt_ID_End()
{
	if (m_rem_ui16 < -1)
	{ throw new Exception("Write_WS_Buffer.Wrt_ID_End() : 原因不明。バッファ不足で ID.End の書き込みができない。"); }

	m_ary_buf[m_idx_byte] = (byte)ID.End;
	m_ary_buf[m_idx_byte + 1] = 0;
	m_idx_byte += 2;
	m_rem_ui16--;
}

// ------------------------------------------------------------------------------------
public void Expand_WS_buf()
{
	m_rem_ui16 += (WS_Buf_Pool.Expand_MemBlk(ref m_ary_buf) >> 1);
	if (m_idx_byte + (m_rem_ui16 << 1) != m_ary_buf.Length)  //// エラー顕在化
	{ throw new Exception("Write_WS_Buffer.Expand_WS_buf() : m_idx_byte + (m_rem_ui16 << 1) != m_ary_buf.Length"); }
}

// ------------------------------------------------------------------------------------
public void Wrt_ID(ID id)
{
	if (m_rem_ui16 <= 0) { this.Expand_WS_buf(); }

	m_ary_buf[m_idx_byte] = (byte)id;
	m_ary_buf[m_idx_byte + 1] = 0;
	m_idx_byte += 2;
	m_rem_ui16--;
}

// ------------------------------------------------------------------------------------
public void Wrt_ID_param(ID id, byte param)
{
	if (m_rem_ui16 <= 0) { this.Expand_WS_buf(); }

	m_ary_buf[m_idx_byte] = (byte)id;
	m_ary_buf[m_idx_byte + 1] = param;
	m_idx_byte += 2;
	m_rem_ui16--;
}

// ------------------------------------------------------------------------------------
// Text では、ID, param の書き込みが後にずれる
public void Skip_Wrt_ID()
{
	if (m_rem_ui16 <= 0) { this.Expand_WS_buf(); }

	m_idx_byte += 2;
	m_rem_ui16--;
}

public void Wrt_ID_param_At(int idx_byte, ID id, byte param)
{
	m_ary_buf[idx_byte] = (byte)id;
	m_ary_buf[idx_byte + 1] = param;
}

// ------------------------------------------------------------------------------------
// ID.Text が自動的に書き込まれる（ID_Text + 文字列長）
public void Wrt_PStr(string src_str)
{
	int len_str = src_str.Length;
	if (m_rem_ui16 < len_str + 2) { this.Expand_WS_buf(); }

	unsafe 
	{
		fixed (char* psrc_top = src_str)
		fixed (byte* pdst_top = m_ary_buf)
		{
			char* psrc = psrc_top;
			char* pdst = (char*)(pdst_top + m_idx_byte);

			*pdst = (char)ID.Text;
			*(pdst + 1) = (char)len_str;

			pdst += 2;
			for (; len_str > 0; --len_str)
			{ *pdst++ = *psrc++; }

			m_idx_byte = (int)(((byte*)pdst) - pdst_top);
			m_rem_ui16 = (m_ary_buf.Length - m_idx_byte) >> 1;
		}
	}
}

// ------------------------------------------------------------------------------------
// ID.Text が自動的に書き込まれる（ID_Text + 文字列長）
// 最後の「/」以降のみが記録される
public void Wrt_PFName(string src_str)
{
	int len_str = src_str.Length;
	if (m_rem_ui16 < len_str + 2) { this.Expand_WS_buf(); }
			
	unsafe 
	{
		fixed (char* psrc_top = src_str)
		fixed (byte* pdst_top = m_ary_buf)
		{
			char* psrc_tmnt = psrc_top + len_str;
			char* pdst = (char*)(pdst_top + m_idx_byte);

			// まず、最後のセパレータを探す
			char* psrc = psrc_tmnt;
			while (*--psrc != '/') {}
			char len_to_wrt = (char)(psrc_tmnt - ++psrc);

			*pdst = (char)ID.Text;
			*(pdst + 1) = (char)len_to_wrt;

			pdst += 2;
			for (; len_to_wrt > 0; --len_to_wrt)
			{ *pdst++ = *psrc++; }

			m_idx_byte = (int)(((byte*)pdst) - pdst_top);
		}
	}
	m_rem_ui16 = (m_ary_buf.Length - m_idx_byte) >> 1;
}

// ------------------------------------------------------------------------------------
// ID_DirFileList (param: path_depth) + ID_Text (path_dir)
// old : ID +「ID_Text / path_dir」＋「ID_Text / path_dir の１つ上の親ディレクトリ（path_depth > 0 のとき）」
public unsafe void Wrt_ID_with_DirDath(ID id, string path_dir)
{
	int len_to_wrt = path_dir.Length * 2 + 3;  // +3 : id_param + id_text + 文字数
	if (m_rem_ui16 < len_to_wrt) { this.Expand_WS_buf(); }

	fixed (byte* pary_buf_top = m_ary_buf)
	fixed (char* ppath_dir_top = path_dir)
	{
		// ID.DirFileList 書き込みは、path depth が分かってから書き込む
		char* pdst = (char*)(pary_buf_top + m_idx_byte + 2);
		*pdst = (char)ID.Text;
		*(pdst + 1) = (char)path_dir.Length;
		*(uint*)(pdst + 2) = 0x002f_002e;  // 「./」の書き込み
		pdst += 4;

		// path_dir の書き込み
		char* psrc = ppath_dir_top + 2;  // 「./」を skip
		char* psrc_at_slash_next_prev = null;  // 親ディレクトリ検出用
		char* psrc_at_slash_next_new = psrc;  // = ppath_dir_top + 2;
		ushort cnt_depth = 0;
		while (true)
		{
			char chr = *psrc++;
			if (chr == 0) { break; }
			if (chr == '/') {
				psrc_at_slash_next_prev = psrc_at_slash_next_new;
				psrc_at_slash_next_new = psrc;
				cnt_depth++;
			}
			*pdst++ = chr;
		}

		// id と path_depth の書き込み
		*(ushort*)(pary_buf_top + m_idx_byte) = (ushort)((cnt_depth << 8) + (ushort)ID.DirFileList);

		m_idx_byte = (int)(((byte*)pdst) - pary_buf_top);
		m_rem_ui16 = (m_ary_buf.Length - m_idx_byte) >> 1;
	}
}

// ------------------------------------------------------------------------------------
public unsafe void Wrt_Num_int(int num)  // ID + 4 bytes の書き込み（ui16 -> ３文字）
{
	if (m_rem_ui16 < 3) { this.Expand_WS_buf(); }

	fixed (byte* pary_buf_top = m_ary_buf)
	{
		byte* pdst = pary_buf_top + m_idx_byte;
		*(ushort*)pdst = (ushort)ID.Num_int;
		*(int*)(pdst + 2) = num;
	}

	m_idx_byte += 6;
	m_rem_ui16 -= 3;
}

// ------------------------------------------------------------------------------------
public void Copy_bytes_from(byte[] ary_buf_src)
{
	if ((ary_buf_src.Length & 1) == 1)
	{
		// idx_byte が奇数である場合はエラーとする
		throw new Exception("Write_WS_Buffer.Set_idx_byte() : ary_buf_src.Length が奇数となっています。");
	}

	int len_src_u16 = (ary_buf_src.Length >> 1);
	while (m_rem_ui16 < len_src_u16)  // ここでは、大きなサイズのコピーが発生しても大丈夫なようにしている
	{
		// Expand_MemBlk の戻り値は、増加された「バイト数」
		m_rem_ui16 += (WS_Buf_Pool.Expand_MemBlk(ref m_ary_buf) >> 1);
		if (m_idx_byte + (m_rem_ui16 << 1) != m_ary_buf.Length)  //// エラー顕在化
		{ throw new Exception("Write_WS_Buffer.Copy_bytes_from() : m_idx_byte + m_rem_ui16 != m_ary_buf.Length"); }
	}

	// m_rem_ui16 の値は先に決定しておく
	m_rem_ui16 -= len_src_u16;

	unsafe
	{
		fixed (byte* pdst_byte_top = m_ary_buf)
		fixed (byte* psrc_byte_top = ary_buf_src)
		{
			ushort* pdst_u16 = (ushort*)(pdst_byte_top + m_idx_byte);
			ushort* psrc_u16 = (ushort*)psrc_byte_top;

			for (; len_src_u16 > 0; --len_src_u16)
			{ *pdst_u16++ = *psrc_u16++; }

			m_idx_byte = (int)(((byte*)pdst_u16) - pdst_byte_top);
		}
	}
}

// ------------------------------------------------------------------------------------
const int EN_MAX_ERR_Text = 200;

// m_ary_buf に書き込まれる内容
// ID_ERR_Report -> ID_Div -> ID_Text -> err_msg -> ID_Div -> ID_Text -> psrc からの EN_MAX_ERR_Text 文字数分の内容
// err_msg は、クライアントと、サーバー側の StdOut に送られるメッセージ
// psrc は MD ファイル側のポインタで fixed されている

// 注意： バッファの拡張をする場合があるため、m_ary_buf は fixed されていてはならない
public unsafe void CrtErrReport_to_WrtBuf_and_ThrowErr(ushort* psrc, string err_msg)
{
	// +10 は念のためのマージン
	int max_len_u16_to_wrt_buf = 1 + 1 + 2 + err_msg.Length + 1 + 2 + EN_MAX_ERR_Text + 10;
	if (m_rem_ui16 < max_len_u16_to_wrt_buf)
	{
		// Expand_MemBlk の戻り値は、増加された「バイト数」
		m_rem_ui16 += (WS_Buf_Pool.Expand_MemBlk(ref m_ary_buf) >> 1);
		if (m_idx_byte + (m_rem_ui16 << 1) != m_ary_buf.Length)  //// エラー顕在化
		{ throw new Exception(
				"Write_WS_Buffer.CrtErrReport_to_WrtBuf_and_ThrowErr() : m_idx_byte + m_rem_ui16 != m_ary_buf.Length"); }
	}
	
	unsafe
	{
		fixed (byte* pdst_top = m_ary_buf)
		{
			ushort* pdst = (ushort*)(pdst_top + m_idx_byte);

			// err_msg の埋め込み
			{
				int len_err_msg = err_msg.Length;

				*pdst = (ushort)ID.ERR_Report;  // このブロックで８バイト
				*(pdst + 1) = (ushort)ID.Div;
				*(pdst + 2) = (ushort)ID.Text;
				*(pdst + 3) = (ushort)len_err_msg;
				pdst += 4;
			
				fixed (char* pmsg_top = err_msg)
				{
					ushort* pmsg = (ushort*)pmsg_top;
					for (; len_err_msg > 0; --len_err_msg)
					{ *pdst++ = *pmsg++; }
				}
			}

			// エラーを特定しやすいように、クライアントに psrc からの文字列を送出する
			// psrc は '\0' で終了していることに留意する
			ushort* pTmnt_dst = pdst + EN_MAX_ERR_Text;

			*pdst = (ushort)ID.Div;
			*(pdst + 1) = (ushort)ID.Text;
			ushort* ptr_at_pos_text_len = pdst + 2;
			pdst += 3;

			while (true)
			{
				ushort chr = *psrc++;
				if (pdst == pTmnt_dst || chr == 0) { break; }
/*
				if (chr == Chr.CR) { *pdst++ = (ushort)ID.BR;  psrc++;  continue; }
				if (chr == Chr.LF) { *pdst++ = (ushort)ID.BR;  continue; }
*/
				*pdst++ = chr;
			}

			*ptr_at_pos_text_len = (ushort)((pdst - ptr_at_pos_text_len) - 1);  // 文字数の書き込み

			m_idx_byte = (int)(((byte*)pdst) - pdst_top);
			m_rem_ui16 = (m_ary_buf.Length - m_idx_byte) >> 1;
		}
	}

	this.Wrt_ID_End();
	throw new Exception(err_msg);
}

// ------------------------------------------------------------------------------------
// １行分の文字数をカウントして、バッファを確保する。
// 必要となる最大バッファサイズは、
// 文字数＋３（先頭の ID_Text or ID_TR, ID_TD, ID_Text）＋１（` の未ペアがあった場合）
// ＋２＊「| の個数（TD、ID_Text）」＋(Chr.PCS_Tab - 1)＊TABの個数
public unsafe void SecureBuf_for1Line(ushort* psrc)
{
	ushort* psrc_top = psrc;
	int pcs_add = 0;
	while (true)
	{
		switch (*psrc)
		{
		case Chr.CR:
		case Chr.LF:
			goto LOOP_OUT;

		case Chr.TAB:
			pcs_add += (Chr.PCS_Tab - 1);
			break;

		case '|':
			pcs_add += 2;
			break;
		}
		psrc++;
	}
LOOP_OUT:
	// 本当は +4 でいいが、余裕をみて +10 としている
	int pcs_to_need = (int)(psrc - psrc_top) + 10 + pcs_add;

	if (m_rem_ui16 < pcs_to_need)
	{
		m_rem_ui16 += (WS_Buf_Pool.Expand_MemBlk(ref m_ary_buf) >> 1);
		if (m_idx_byte + (m_rem_ui16 << 1) != m_ary_buf.Length)  //// エラー顕在化
		{ throw new Exception("Write_WS_Buffer.SecureBuf_for1Line() : m_idx_byte + m_rem_ui16 != m_ary_buf.Length"); }
	}
}

// ------------------------------------------------------------------------------------
// Code ブロック用（今後、大幅に加筆される予定あり）
// 戻り値 : 次の行頭
public unsafe ushort* Cosume_CodeLine(ushort* psrc)
{
	// 先頭が改行（空行）であった場合、改行のみしてリターンする
	if (*psrc == Chr.CR) { this.Wrt_ID(ID.BR);  return psrc + 2; }
	if (*psrc == Chr.LF) { this.Wrt_ID(ID.BR);  return psrc + 1; }

	this.SecureBuf_for1Line(psrc);

	// エラーのスローは、エラーが発生したところまで書き込んだ後に行うようにする
	string Err_msg = null;

	// Text ブロックの書き込み
	// dest 側の fixed
	fixed (byte* pdst_top = m_ary_buf)
	{
		ushort* pdst = (ushort*)(pdst_top + m_idx_byte);
		ushort* DBG_pTmnt_dst = pdst + m_rem_ui16;  // エラー顕在化のため

		*pdst = (ushort)ID.Text;
		ushort* pdst_at_pos_text_len = pdst + 1;
		pdst += 2;

		while (true)
		{
			ushort chr = *psrc++;
			// 行末が来たら処理を終了する。ID.BR の処理は Lexer の方で行う
			if (chr == Chr.CR) { psrc++;  break; }
			if (chr == Chr.LF) { break; }

			if (chr == Chr.TAB)
			{						
				for (int i = Chr.PCS_Tab; i > 0; --i) { *pdst++ = Chr.SP; }
				continue;
			}

			*pdst++ = chr;
		}

		*pdst_at_pos_text_len = (ushort)((pdst - pdst_at_pos_text_len) - 1);  // 文字数の書き込み

		if (pdst > DBG_pTmnt_dst)  // 発生することはないはずだけど
		{
			Err_msg = "Write_WS_Buffer.Cosume_CodeLine() : バッファオーバーフローが発生しました。";
		}
		else
		{
			m_idx_byte = (int)(((byte*)pdst) - pdst_top);
			m_rem_ui16 = (m_ary_buf.Length - m_idx_byte) >> 1;
		}
	}

	// 今のところは、ERR_OVERFLOW 以外のエラーは起きないはず
	if (Err_msg != null) { this.CrtErrReport_to_WrtBuf_and_ThrowErr(psrc, Err_msg); }

	// CodeBlk は必ず改行コードで終える
	this.Wrt_ID(ID.BR);
	return psrc;
}

// ------------------------------------------------------------------------------------
byte m_Txt_flags = 0;

public void Clear_Txt_flags() { m_Txt_flags = 0; }

// ------------------------------------------------------------------------------------
// 行末コード CR or LF に達したところでリターンする
// Normal ブロック用（今後、大幅に加筆される予定あり）
// 戻り値 : 行末記号の位置（CR or LF）
// テーブルの生成時には、Consume_NormalLine() は特殊な動作を行う\ (--- の読み飛ばしなどを実装するため)
public unsafe ushort* Consume_NormalLine(ushort* psrc)
{
	this.SecureBuf_for1Line(psrc);

	// エラーのスローは、エラーが発生したところまで書き込んだ後に行うようにする
	string Err_msg = null;

	// dest 側の fixed
	fixed (byte* pdst_top = m_ary_buf)
	{
		ushort* pdst = (ushort*)(pdst_top + m_idx_byte);
		ushort* DBG_pTmnt_dst = pdst + m_rem_ui16;  // エラー顕在化のため

		// ptr_at_pos_text_len が null のときは、テキストセクションの処理外であると分かる
		ushort* ptr_at_pos_text_len = null;
		bool b_on_escaped = false;

		void CLOSE_TEXT_SEC_IF_OPEN()
		{
			if (ptr_at_pos_text_len != null)
			{
				ushort len_text = (ushort)(pdst - ptr_at_pos_text_len - 1);
				if (len_text == 0)
				{
					pdst -= 2;
				}
				else
				{
					*ptr_at_pos_text_len = len_text;
				}
				ptr_at_pos_text_len = null;
			}
		}

		// -------------------------------------------------
		// １文字目が '|' の場合、表であるものとして処理をする
		bool b_is_table = false;
//		ushort* ptr_last_vl = null;
		if (*psrc == '|')
		{
			b_is_table = true;

			// psrc の最後の | は、Chr.LF にしておく
			// また、「---」を見つけたら、m_idx_byte, m_rem_ui16 の両方とも変更せずに、psrc を次の行頭にして return する
			ushort* ptr_last_vl = psrc++;  // psrc は「|」の次を指す
			ushort* ptr = psrc;
			while (true)
			{
				switch (*ptr)
				{
				case Chr.CR:
				case Chr.LF:
					goto LOOP_OUT;

				case '|':
					ptr_last_vl = ptr;
					break;

				case '-':
					if (*(uint*)(ptr + 1) == 0x002d_002d)  //「---」であった場合、その行の読み込みは実行しない
					{
						ptr += 3;
						while(*ptr++ != Chr.LF) {}
						return ptr;  // ここでリターンしてしまう
					}
					break;
				}
				ptr++;
			}

		LOOP_OUT:
			*ptr_last_vl = Chr.LF;

			*pdst = (ushort)ID.TR;
			*(pdst + 1) = (ushort)ID.TD;
			pdst += 2;
		}
		// -------------------------------------------------

		while (true)  // 行末 or エラー発生時まで処理を進める
		{
			ushort chr = *psrc;

			// 行末処理で、改行が必要かどうかの判断を行うため、psrc は最初の行末コードの部分でリターンさせる
			if (chr == Chr.CR || chr == Chr.LF) { break; }

			//  -------------------------------------------------
			// エスケープ文字の処理
			if (b_on_escaped == true)
			{
				b_on_escaped = false;  // エスケープ解除
			}
			else
			{
				// 以下、特殊 inline 文字の処理を順次行う
				switch (chr)
				{
				case '\\':
					b_on_escaped = true;
					psrc++;
					continue;  // '\' は書き込まない（*pdst++ = chr; をしない）

				case '<':
					ulong ui64str = *(ulong*)psrc;
					if (ui64str == 0x003e_0072_0062_003c || ui64str == 0x003e_0052_0042_003c)
					{
						// <br>, <BR> の処理
						CLOSE_TEXT_SEC_IF_OPEN();
						*pdst++ = (char)ID.BR;
						psrc += 4;
						continue;
					}
					break;

				case '*':
					if (*(psrc + 1) == '*')
					{
						// ボールド処理（トグル）
						CLOSE_TEXT_SEC_IF_OPEN();
						m_Txt_flags ^= (byte)ID.Txt_Bold;
						psrc += 2;
						continue;
					}
					break;

				case '~':
					if (*(psrc + 1) == '~')
					{
						// キャンセル処理（トグル）
						CLOSE_TEXT_SEC_IF_OPEN();
						m_Txt_flags ^= (byte)ID.Txt_Cancel;
						psrc += 2;
						continue;
					}
					break;

				case '_':
					if (*(psrc + 1) == '_')
					{
						// 下線処理（トグル）
						CLOSE_TEXT_SEC_IF_OPEN();
						m_Txt_flags ^= (byte)ID.Txt_Under;
						psrc += 2;
						continue;
					}
					break;

				case '`':
					// Code 処理（トグル）
					CLOSE_TEXT_SEC_IF_OPEN();
					m_Txt_flags ^= (byte)ID.Txt_Code;
					psrc++;
					continue;

				case '|':
					if (b_is_table == false) { break; }
					// TD の処理
					CLOSE_TEXT_SEC_IF_OPEN();
					*pdst++ = (ushort)ID.TD;

					psrc++;
					continue;
				} // switch
			} // else

			//  -------------------------------------------------
			// chr は、何らかの表示文字であると確定
			if (ptr_at_pos_text_len == null)
			{
				// テキストセクションを開く（この時点では、param は必ず 0 となる）
				*pdst = (ushort)((byte)ID.Text | m_Txt_flags);
				ptr_at_pos_text_len = pdst + 1;
				pdst += 2;
			}

			psrc++;
			
			if (chr == Chr.TAB)
			{
				for (int i = Chr.PCS_Tab; i > 0; --i) { *pdst++ = Chr.SP; }
				continue;
			}

			*pdst++ = chr;
		} // while

		// text セクションが 0 文字となることもある（エラー時など）
		CLOSE_TEXT_SEC_IF_OPEN();

		// 表作成時に書き換えていたものを、元に戻す（行末処理の改行と間違えないようにするため）
		if (b_is_table == true)
		{
//			*ptr_last_vl = '|';
//			if (psrc != ptr_last_vl)  // エラー顕在化
//			{ Err_msg = "Write_WS_Buffer.Consume_NormalLine() : 表の作成中に、psrc != ptr_last_vl が発生しました。"; }

			// テーブルの生成時には、戻り値には次の行頭を渡す
			psrc++;  // psrc は、元「|」であったところの LF の次となる
			while (*psrc++ != Chr.LF) {}
		}

		if (pdst > DBG_pTmnt_dst)  // 発生することはないはずだけど
		{
			Err_msg = "Write_WS_Buffer.Consume_NormalLine() : バッファオーバーフローが発生しました。";
		}
		else
		{
			m_idx_byte = (int)(((byte*)pdst) - pdst_top);
			m_rem_ui16 = (m_ary_buf.Length - m_idx_byte) >> 1;
		}
	}

	// 今のところ、ERR_OVERFLOW 以外のエラーは起きない
	if (Err_msg != null) { this.CrtErrReport_to_WrtBuf_and_ThrowErr(psrc, Err_msg); }
	return psrc;
}

// ------------------------------------------------------------------------------------
// [text] の結合、[text] の行末の空白削除
// エラーがあった場合、エラー事由が返される。（エラーがなければ null が返される）
public unsafe void Simplify_Buf(int idx_byte_lex_top)
{
	try
	{
		fixed (byte* ary_buf_top = m_ary_buf)
		{
			Text_section_compaction(idx_byte_lex_top);

//			DBG_WS_Buffer.Show_WS_buf(MainForm.ms_RBox_stdout, m_ary_buf, m_idx_byte);

			// -------------------------------------------------------
			// その他のコンパクションをする場合は、この位置で行うこと

			// -------------------------------------------------------
			// FLG_no_BR_above の処理（データのバイト数は変改しない）
			ushort* psrc = (ushort*)(ary_buf_top + idx_byte_lex_top);
			ushort* pTmnt_src = (ushort*)(ary_buf_top + m_idx_byte);
			bool b_next_is_no_BR_above = false;

			bool b_in_QuoteBlk = false;

			// psrc の最初は ID.Lexed_MD。その次は、必ず Div系のブロックのはず
			*(psrc + 1) |= Param.FLG_no_BR_above_ushort;
			psrc += 2;
			while (psrc < pTmnt_src)
			{
				ushort val_ushort = *psrc;
				ID id = (ID)val_ushort;

				if (id.IsText())
				{
					// SP を nbsp へ変換
					int param = val_ushort >> 8;
					int text_len = (param > 0) ? param : *(++psrc);
					for (; text_len > 0; --text_len)
					{
						ushort c = *(++psrc);
						if (c == Chr.SP) { *psrc = Chr.NBSP; }
					}
					psrc++;
				}
				else if (id == ID.Div_Quote)
				{
					b_in_QuoteBlk = !b_in_QuoteBlk;
					if (b_in_QuoteBlk == true)
					{ b_next_is_no_BR_above = true; }
					else
					{ b_next_is_no_BR_above = false; }  // 空の QuoteBlk だった場合（ないはずだけど、、）
					psrc++;
				}
				else
				{
					if (b_next_is_no_BR_above == true)
					{
						if (id.IsDiv() == false)  // バグの顕在化
						{ throw new Exception("Div_Quote の直後が Divブロックではありませんでした。"); }

						*psrc |= Param.FLG_no_BR_above_ushort;
						b_next_is_no_BR_above = false;
					}
					psrc++;
				}
			} // while
		} // fixed

//		DBG_WS_Buffer.Show_WS_buf(MainForm.ms_RBox_stdout, m_ary_buf, m_idx_byte);

	}
	catch(Exception ex)
	{
		this.Wrt_ID(ID.ERR_on_Simplify);
		this.Wrt_PStr(ex.Message);
		throw;  // rethrow
	}
}

// ------------------------------------------------------------------------------------
public unsafe void Text_section_compaction(int idx_byte_lex_top)
{
	fixed (byte* ary_buf_top = m_ary_buf)
	{
		ushort* psrc = (ushort*)(ary_buf_top + idx_byte_lex_top);
		ushort* pTmnt_src = (ushort*)(ary_buf_top + m_idx_byte);
		ushort* pdst = psrc;

		// ptr_at_pos_text_len != null であるときは、text セクションが閉じていない、ということ
		ushort* ptr_at_pos_text_len = null;
		// 最初の text セクションが開かれるときに、値が設定される（param は常に 0）
		ushort cur_ID_Txt_with_flags = 0;

		void CLOSE_TEXT_SEC()
		{
			ushort text_len = (ushort)(pdst - ptr_at_pos_text_len - 1);
			if (text_len == 0)
			{
				pdst = ptr_at_pos_text_len - 1;
			}
			else if (text_len > 0xff)
			{
				*ptr_at_pos_text_len = text_len;
			}
			else  // text_len <= 255 のときの処理
			{
				*(ptr_at_pos_text_len - 1) = (ushort)((text_len << 8) + cur_ID_Txt_with_flags);
				ushort* p_org = ptr_at_pos_text_len + 1;
				for (; text_len > 0; --text_len)
				{ *(p_org - 1) = *p_org++; }

				pdst--;
			}
			ptr_at_pos_text_len = null;
		}

		// ---------------------------------------------------------
		// compaction 開始
		ID id_prev = ID.Undefined;
		while (psrc < pTmnt_src)
		{
			ushort chr = *psrc++;

			if (((ID)chr).IsText())
			{
				if (ptr_at_pos_text_len == null)
				{
					// text セクションを開く
					*pdst = chr;  // Txt_flags もそのまま書き込む（param は必ず 0）
					cur_ID_Txt_with_flags = chr;
					ptr_at_pos_text_len = pdst + 1;
					pdst += 2;
				}
				else
				{
					// text セクションが連続するときの処理（Txt_flags に変更がないかを確認）
					if (chr != cur_ID_Txt_with_flags)
					{
						// Txt_flags に変更があった場合の処理
						CLOSE_TEXT_SEC();

						// text セクションを新しく開く
						*pdst = chr;  // Txt_flags もそのまま書き込む（param は必ず 0）
						cur_ID_Txt_with_flags = chr;
						ptr_at_pos_text_len = pdst + 1;
						pdst += 2;
					}
				}

				// Text セクションの１つ前が ID_TD であったら、セクションの行頭にある空白を削除する
				if (id_prev == ID.TD)
				{
					int rem_txt = *psrc++;
					while (true)
					{
						if (rem_txt == 0) { goto FINISHD_TEXT_COPY; }
						if (*psrc != Chr.SP) { break; }
						psrc++;
						rem_txt--;
					}
					for (; rem_txt > 0; --rem_txt)
					{ *pdst++ = *psrc++; }
				}
				else
				{
					// テキスト全部のコピー
					for (int i = *psrc++; i > 0; --i)
					{ *pdst++ = *psrc++; }
				}

			FINISHD_TEXT_COPY:
				id_prev = ID.Text;
			}
			else  // text セクションでない場合の処理（End, ERR, BR, Div, TR, TD）
			{
				// text セクションが開いていた場合、それを閉じる
				if (ptr_at_pos_text_len != null)
				{
					//「装飾がない」行末の空白文字を削除
					if ((byte)*(ptr_at_pos_text_len - 1) == (byte)ID.Text)
					{
						for (long i = pdst - ptr_at_pos_text_len -1; i > 0; --i)  // text_len == 0 のときを考慮
						{
							if (*(pdst - 1) == Chr.SP) { pdst--; continue; }

//							ushort c = *(pdst - 1);
//							if (c == Chr.SP || c == Chr.SP_ZEN) { pdst--; continue; }

							break;
						}
					}

					CLOSE_TEXT_SEC();
				}

				*pdst++ = chr;  // chr は ID.Txt を持たない
				id_prev = (ID)chr;
			}
		}  // while

		if (psrc > pTmnt_src)
		{ throw new Exception("!!! Text_section_compaction() : psrc > pTmnt_src となりました。"); }

		int chr_end_id = *(psrc - 1) & 0xff;
		if (chr_end_id != (int)ID.End)
		{ throw new Exception($"!!! Text_section_compaction() : chr_end = {chr_end_id.ToString()}"); }

		// -------------------------------------------------------
		// 終了処理 : m_idx_byte, m_rem_ui16 を再設定する
		m_idx_byte = (int)(((byte*)pdst) - ary_buf_top);
		m_rem_ui16 = (m_ary_buf.Length - m_idx_byte) >> 1;
	}
}


}  // Write_WS_Buffer
}  // namespace md_svr
