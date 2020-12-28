using System;
using System.Text;

namespace md_svr
{
class Read_WS_Buf
{
public static UnicodeEncoding ms_utf16_encoding;

byte[] m_buf;

int m_idx_byte = 0;
int m_rem_ui16 = 0;  // コンストラクト時には、読み取りバッファは 0 であるはず

byte m_param_cur = 0;
string m_text_cur = null;

// ------------------------------------------------------------------------------------
internal Read_WS_Buf(byte[] buf)
{
	m_buf = buf;
}

// ------------------------------------------------------------------------------------
// 読み取りバッファが空になっている場合は、0 が返される
public byte Read_ID() 
{
	if (m_rem_ui16 == 0) { return 0; }

	byte id_ret = m_buf[m_idx_byte];
	m_param_cur = m_buf[m_idx_byte + 1];

	m_idx_byte += 2;
	m_rem_ui16--;

	if (id_ret == (byte)ID.Text)
	{
		if (m_rem_ui16 == 0)
		{ throw new Exception("Read_WS_Buf.Read_ID() : m_rem_ui16 == 0"); }
				
		int len_txt = m_buf[m_idx_byte] + (m_buf[m_idx_byte + 1] << 8);
		if (len_txt >= m_rem_ui16)
		{ throw new Exception("Read_WS_Buf.Read_ID() : len_txt >= m_rem_ui16"); }

		// 第２引数、第３引数ともに、バイト数で表していることに注意
		m_text_cur = ms_utf16_encoding.GetString(m_buf, m_idx_byte + 2, len_txt << 1);

		m_idx_byte += 2 + (len_txt << 1);
		m_rem_ui16 -= len_txt + 1;
	}
	else
	{
		m_text_cur = null;
	}

	return id_ret;
}

// ------------------------------------------------------------------------------------
public byte Get_param_cur() => m_param_cur;
public string Get_text_cur() => m_text_cur;

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

internal class Write_WS_Buffer
{
byte[] m_buf;

int m_idx_byte;
int m_rem_ui16;

const int EN_Margin_buf = 10;  // TAB、テキストセクションの生成などのマージン

// ------------------------------------------------------------------------------------
public Write_WS_Buffer(byte[] buf)
{
	m_buf = buf;

	m_idx_byte = 0;
	m_rem_ui16 = (buf.Length >> 1) - 2;  // エラー報告と、ID.End を必ず書き込めるようにするため
}

// ------------------------------------------------------------------------------------
public byte[] Get_buf() => m_buf;
public int Get_idx_byte_cur() => m_idx_byte;

// ------------------------------------------------------------------------------------
public void Flush()
{
	m_idx_byte = 0;
	m_rem_ui16 = (m_buf.Length >> 1) - 2;  // エラー報告と、ID.End を必ず書き込めるようにするため
}

// ------------------------------------------------------------------------------------
public void Wrt_ID_End()
{
	if (m_rem_ui16 < -1)
	{ throw new Exception("原因不明。バッファ不足で ID.End の書き込みができない。"); }

	m_buf[m_idx_byte] = (byte)ID.End;
	m_buf[m_idx_byte + 1] = 0;
	m_idx_byte += 2;
	m_rem_ui16--;
}

// ------------------------------------------------------------------------------------
public void Wrt_ID(ID id)
{
	if (m_rem_ui16 <= 0) { this.THROW_Overflow_ERR(); }

	m_buf[m_idx_byte] = (byte)id;
	m_buf[m_idx_byte + 1] = 0;
	m_idx_byte += 2;
	m_rem_ui16--;
}

// ------------------------------------------------------------------------------------
public void Wrt_ID_param(ID id, byte param)
{
	if (m_rem_ui16 <= 0) { this.THROW_Overflow_ERR(); }

	m_buf[m_idx_byte] = (byte)id;
	m_buf[m_idx_byte + 1] = param;
	m_idx_byte += 2;
	m_rem_ui16--;
}

// ------------------------------------------------------------------------------------
// Text では、ID, param の書き込みが後にずれる
public void Skip_Wrt_ID()
{
	m_idx_byte += 2;
	m_rem_ui16--;
}

public void Wrt_ID_param_At(int idx_byte, ID id, byte param)
{
	m_buf[idx_byte] = (byte)id;
	m_buf[idx_byte + 1] = param;
}

// ------------------------------------------------------------------------------------
// ID.Text が自動的に書き込まれる
public void Wrt_PStr(string src_str)
{
	if (m_rem_ui16 < EN_Margin_buf) { this.THROW_Overflow_ERR(); }

	int len_to_wrt = Math.Min(src_str.Length, m_rem_ui16 - 3);
	unsafe 
	{
		fixed (char* psrc_top = src_str)
		fixed (byte* pdst_top = m_buf)
		{
			char* psrc = psrc_top;
			char* pdst = (char*)(pdst_top + m_idx_byte);

			*pdst = (char)ID.Text;
			*(pdst + 1) = (char)len_to_wrt;

			pdst += 2;
			for (; len_to_wrt > 0; --len_to_wrt)
			{ *pdst++ = *psrc++; }

			m_idx_byte = (int)(((byte*)pdst) - pdst_top);
			m_rem_ui16 = (m_buf.Length - m_idx_byte) >> 1;
		}
	}
}

// ------------------------------------------------------------------------------------
// ID.Text が自動的に書き込まれる
// 最後の「/」以降のみが記録される
public void Wrt_PFName(string src_str)
{
	int len_str = src_str.Length;
	if (m_rem_ui16 < len_str + 2)  // +2 : ID_Text と 文字列長
	{ this.THROW_Overflow_ERR(); }
			
	unsafe 
	{
		fixed (char* psrc_top = src_str)
		fixed (byte* pdst_top = m_buf)
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
	m_rem_ui16 = (m_buf.Length - m_idx_byte) >> 1;
}

// ------------------------------------------------------------------------------------
public void THROW_Overflow_ERR()
{
	if (m_rem_ui16 >= 0)
	{
		// この場合は、バッファにエラー報告を書き入れる余地がまだある
		m_buf[m_idx_byte] = (byte)ID.ERR_OVERFLOW;
		m_buf[m_idx_byte + 1] = 0;
		m_idx_byte += 2;
		m_rem_ui16--;
	}

	if (m_rem_ui16 >= -1)
	{
		// この場合は、バッファに End を書き入れる余地がまだある
		m_buf[m_idx_byte] = (byte)ID.End;
		m_buf[m_idx_byte + 1] = 0;
		m_idx_byte += 2;
		m_rem_ui16--;
	}

	throw new Exception("Write_WS_Buffer が不足しています。");
}

// ------------------------------------------------------------------------------------
const int EN_MAX_ERR_Text = 200;

// エラーレポートと、ID.End を書き込んで例外を投げる
// err_msg は、クライアントと、サーバー側の StdOut に送られるメッセージ
public unsafe void THROW_ERR(char* psrc, string err_msg)
{
	if (m_rem_ui16 < EN_Margin_buf) { this.THROW_Overflow_ERR(); }

	fixed (byte* pdst_top = m_buf)
	{
		char* pdst = (char*)(pdst_top + m_idx_byte);

		// err_msg の埋め込み
		{
			int len_to_wrt = err_msg.Length;
			if (m_rem_ui16 < 4 + len_to_wrt) { len_to_wrt = m_rem_ui16 - 4; }

			m_idx_byte += (4 + len_to_wrt) * 2;
			m_rem_ui16 -= 4 + len_to_wrt;

			*pdst = (char)ID.ERR_Report;
			*(pdst + 1) = (char)ID.Div;
			*(pdst + 2) = (char)ID.Text;
			*(pdst + 3) = (char)len_to_wrt;
			pdst += 4;
			
			fixed (char* pmsg_top = err_msg)
			{
				char* pmsg = pmsg_top;
				for (; len_to_wrt > 0; --len_to_wrt)
				{ *pdst++ = *pmsg++; }
			}
		}

		// psrc からの文字列の書き込み
		if (m_rem_ui16 < 4) { this.THROW_Overflow_ERR(); }

		// +3 : ID.Div、ID.Text、len_text
		char* pTmnt_dst = pdst + Math.Min(EN_MAX_ERR_Text + 3, m_rem_ui16);

		*pdst = (char)ID.Div;
		*(pdst + 1) = (char)ID.Text;
		char* ptr_at_pos_text_len = pdst + 2;

		while (true)
		{
			char chr = *psrc++;
			if (pdst == pTmnt_dst || chr == 0) { break; }

			if (chr == Chr.CR) { *pdst++ = (char)ID.BR;  psrc++;  continue; }
			if (chr == Chr.LF) { *pdst++ = (char)ID.BR;  continue; }

			*pdst++ = chr;
		}

		*ptr_at_pos_text_len = (char)((pdst - ptr_at_pos_text_len) - 1);  // 文字数の書き込み
		m_idx_byte = (int)(((byte*)pdst) - pdst_top);
		m_rem_ui16 = (m_buf.Length - m_idx_byte) >> 1;
	}

	this.Wrt_ID_End();
	throw new Exception(err_msg);
}

// ------------------------------------------------------------------------------------
// 行末コード CR or LF に達したところでリターンする（ID.BR の書き込みはしない）
// Code ブロック用（今後、大幅に加筆される予定）
// 戻り値 : 次の行頭
public unsafe char* Cosume_CodeLine(char* psrc)
{
	// 先頭が改行（空行）であった場合、改行のみしてリターンする
	if (*psrc == Chr.CR) { this.Wrt_ID(ID.BR);  return psrc + 2; }
	if (*psrc == Chr.LF) { this.Wrt_ID(ID.BR);  return psrc + 1; }

	if (m_rem_ui16 < EN_Margin_buf) { this.THROW_Overflow_ERR(); }

	// エラーのスローは、エラーが発生したところまで書き込んだ後に行うようにする
	ID Err_ID = ID.Undefined;
	string Err_msg = null;

	// Text ブロックの書き込み
	// dest 側の fixed
	fixed (byte* pdst_top = m_buf)
	{
		char* pdst = (char*)(pdst_top + m_idx_byte);
		char* pTmnt_dst = pdst + m_rem_ui16 - EN_Margin_buf;

		*pdst = (char)ID.Text;
		char* pdst_at_pos_text_len = pdst + 1;
		pdst += 2;

		while (true)
		{
			if (pdst >= pTmnt_dst)
			{
				Err_ID = ID.ERR_OVERFLOW;
				break;
			}

			char chr = *psrc++;
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

		*pdst_at_pos_text_len = (char)((pdst - pdst_at_pos_text_len) - 1);  // 文字数の書き込み
		m_idx_byte = (int)(((byte*)pdst) - pdst_top);
		m_rem_ui16 = (m_buf.Length - m_idx_byte) >> 1;
	}

	if (Err_ID == ID.ERR_OVERFLOW) { this.THROW_Overflow_ERR(); }
	// 今のところは、ERR_OVERFLOW 以外のエラーは起きないはず
	if (Err_ID != ID.Undefined) { this.THROW_ERR(psrc, Err_msg); }

	// CodeBlk は必ず改行コードで終える
	this.Wrt_ID(ID.BR);
	return psrc;
}

// ------------------------------------------------------------------------------------
// 行末コード CR or LF に達したところでリターンする（ID.BR の書き込みはしない）
// Normal ブロック用（今後、大幅に加筆される予定）
// 戻り値 : 行末記号の位置（CR or LF）
public unsafe char* Consume_NormalLine(char* psrc)
{
	if (m_rem_ui16 < EN_Margin_buf) { this.THROW_Overflow_ERR(); }

		// エラーのスローは、エラーが発生したところまで書き込んだ後に行うようにする
	ID Err_ID = ID.Undefined;
	string Err_msg = null;

	// dest 側の fixed
	fixed (byte* pdst_top = m_buf)
	{
		char* pdst = (char*)(pdst_top + m_idx_byte);
		char* pTmnt_dst = pdst + m_rem_ui16 - EN_Margin_buf;

		// ptr_at_pos_text_len が null のときは、テキストセクションの処理外であると分かる
		char* ptr_at_pos_text_len = null;
		bool b_on_escaped = false;

		void CLOSE_TEXT_SEC_IF_OPEN()
		{
			if (ptr_at_pos_text_len != null)
			{
				*ptr_at_pos_text_len = (char)(pdst - ptr_at_pos_text_len - 1);
				ptr_at_pos_text_len = null;
			}
		}

		while (true)  // 行末 or エラー発生時まで処理を進める
		{
			if (pdst >= pTmnt_dst)
			{
				Err_ID = ID.ERR_OVERFLOW;
				break;
			}

			char chr = *psrc;

			// 行末処理で、改行が必要かどうかの判断を行うため、psrc は最初の行末コードの部分でリターンさせる
			if (chr == Chr.CR || chr == Chr.LF) { break; }

			//  -------------------------------------------------
			// chr は、何らかの表示文字。以降は、text セクションとして処理

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
					// <br>, <BR> の検査
					ulong ui64str = *(ulong*)psrc;
					if (ui64str == 0x003e_0072_0062_003c || ui64str == 0x003e_0052_0042_003c)
					{
						CLOSE_TEXT_SEC_IF_OPEN();

						*pdst++ = (char)ID.BR;
						psrc += 4;
						continue;
					}
					break;
				}
			}

			if (ptr_at_pos_text_len == null)
			{
				// テキストセクションを開く
				*pdst = (char)ID.Text;
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

		m_idx_byte = (int)(((byte*)pdst) - pdst_top);
		m_rem_ui16 = (m_buf.Length - m_idx_byte) >> 1;
	}

	if (Err_ID == ID.ERR_OVERFLOW) { this.THROW_Overflow_ERR(); }
	// 今のところは、ERR_OVERFLOW 以外のエラーは起きないはず
	if (Err_ID != ID.Undefined) { this.THROW_ERR(psrc, Err_msg); }

	return psrc;
}

// ------------------------------------------------------------------------------------
// [text] の結合、[text] の行末の空白削除
// エラーがあった場合、エラー事由が返される。（エラーがなければ null が返される）
public unsafe string Simplify_Buf()
{
	string ret_str;
	fixed (byte* buf_top_byte = m_buf)
	{
		char* psrc = (char*)buf_top_byte;
		char* pTmnt_src = (char*)(buf_top_byte + m_idx_byte);
		char* pdst = (char*)buf_top_byte;
		
		// ptr_at_pos_text_len != null であるときは、text セクションが閉じていない、ということ
		char* ptr_at_pos_text_len = null;
		while (true)
		{
			if (psrc >= pTmnt_src) { break; }

			char chr = *psrc++;
			if ((byte)(chr & 0xff) == (byte)ID.Text)
			{
				if (ptr_at_pos_text_len == null)
				{
					// text セクションを開く
					*pdst = (char)ID.Text;
					ptr_at_pos_text_len = pdst + 1;
					pdst += 2;
				}

				// テキストのコピー
				char* pdst_line_top = pdst;
				for (int i = *psrc++; i > 0; --i)
				{ *pdst++ = *psrc++; }

				// 行末の空白文字を削除
				if (pdst > pdst_line_top)  // text_len == 0 のときを考慮
				{
					while (true)
					{
						switch (*(--pdst))
						{
						case Chr.SP:
						case Chr.SP_ZEN:
							if (pdst == pdst_line_top) { break; }
							continue;

						default:
							pdst++;
							break;
						}
						break;
					}
				}
			}
			else
			{
				// text セクションが開いていた場合、それを閉じる
				if (ptr_at_pos_text_len != null)
				{
					char text_len = (char)(pdst - ptr_at_pos_text_len - 1);
					if (text_len == 0)
					{
						pdst = ptr_at_pos_text_len - 1;
					}
					else
					{
						*ptr_at_pos_text_len = text_len;
					}
					ptr_at_pos_text_len = null;
				}

				*pdst++ = chr;
			}
		}  // while

		// -------------------------------------------------------
		// m_idx_byte, m_rem_ui16 を再設定する
		m_idx_byte = (int)(((byte*)pdst) - buf_top_byte);
		m_rem_ui16 = (m_buf.Length - m_idx_byte) >> 1;

		if (psrc == pTmnt_src)
		{
			char chr_end = *(psrc - 1);
			if ((byte)(chr_end & 0xff) == (byte)ID.End)
			{
				ret_str = null;
			}
			else
			{
				ret_str = $"!!! 終端が ID.End で終わっていませんでした。chr_end = {((int)chr_end).ToString()}";
			}
		}
		else
		{
			ret_str = "!!! Simplify時に、psrc > pTmnt_src となりました。";
		}
	}  // fixed

	if (ret_str != null)
	{
		this.Wrt_ID(ID.ERR_on_Simplify);
		this.Wrt_PStr(ret_str);
	}
	return ret_str;
}


}  // Write_WS_Buffer
}  // namespace md_svr
