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
// Read_ID() は、client から送られてきたデータを読み取るためだけにあることに注意
// text セクションのコンパクションはされていない
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
		if (m_rem_ui16 <= len_txt)
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

public class Write_WS_Buffer
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
public unsafe void THROW_ERR(ushort* psrc, string err_msg)
{
	if (m_rem_ui16 < EN_Margin_buf) { this.THROW_Overflow_ERR(); }

	fixed (byte* pdst_top = m_buf)
	{
		ushort* pdst = (ushort*)(pdst_top + m_idx_byte);

		// err_msg の埋め込み
		{
			int len_to_wrt = err_msg.Length;
			if (m_rem_ui16 < 4 + len_to_wrt) { len_to_wrt = m_rem_ui16 - 4; }

			m_idx_byte += (4 + len_to_wrt) * 2;
			m_rem_ui16 -= 4 + len_to_wrt;

			*pdst = (ushort)ID.ERR_Report;
			*(pdst + 1) = (ushort)ID.Div;
			*(pdst + 2) = (ushort)ID.Text;
			*(pdst + 3) = (ushort)len_to_wrt;
			pdst += 4;
			
			fixed (char* pmsg_top = err_msg)
			{
				ushort* pmsg = (ushort*)pmsg_top;
				for (; len_to_wrt > 0; --len_to_wrt)
				{ *pdst++ = *pmsg++; }
			}
		}

		// psrc からの文字列の書き込み
		if (m_rem_ui16 < 4) { this.THROW_Overflow_ERR(); }

		// +3 : ID.Div、ID.Text、len_text
		ushort* pTmnt_dst = pdst + Math.Min(EN_MAX_ERR_Text + 3, m_rem_ui16);

		*pdst = (ushort)ID.Div;
		*(pdst + 1) = (ushort)ID.Text;
		ushort* ptr_at_pos_text_len = pdst + 2;

		while (true)
		{
			ushort chr = *psrc++;
			if (pdst == pTmnt_dst || chr == 0) { break; }

			if (chr == Chr.CR) { *pdst++ = (ushort)ID.BR;  psrc++;  continue; }
			if (chr == Chr.LF) { *pdst++ = (ushort)ID.BR;  continue; }

			*pdst++ = chr;
		}

		*ptr_at_pos_text_len = (ushort)((pdst - ptr_at_pos_text_len) - 1);  // 文字数の書き込み
		m_idx_byte = (int)(((byte*)pdst) - pdst_top);
		m_rem_ui16 = (m_buf.Length - m_idx_byte) >> 1;
	}

	this.Wrt_ID_End();
	throw new Exception(err_msg);
}

// ------------------------------------------------------------------------------------
// Code ブロック用（今後、大幅に加筆される予定あり）
// 戻り値 : 次の行頭
public unsafe ushort* Cosume_CodeLine(ushort* psrc)
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
		ushort* pdst = (ushort*)(pdst_top + m_idx_byte);
		ushort* pTmnt_dst = pdst + m_rem_ui16 - EN_Margin_buf;

		*pdst = (ushort)ID.Text;
		ushort* pdst_at_pos_text_len = pdst + 1;
		pdst += 2;

		while (true)
		{
			if (pdst >= pTmnt_dst)
			{
				Err_ID = ID.ERR_OVERFLOW;
				break;
			}

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
byte m_Txt_flags = 0;

public void Clear_Txt_flags() { m_Txt_flags = 0; }

// ------------------------------------------------------------------------------------
// 行末コード CR or LF に達したところでリターンする
// Normal ブロック用（今後、大幅に加筆される予定あり）
// 戻り値 : 行末記号の位置（CR or LF）
public unsafe ushort* Consume_NormalLine(ushort* psrc)
{
	if (m_rem_ui16 < EN_Margin_buf) { this.THROW_Overflow_ERR(); }

		// エラーのスローは、エラーが発生したところまで書き込んだ後に行うようにする
	ID Err_ID = ID.Undefined;
	string Err_msg = null;

	// dest 側の fixed
	fixed (byte* pdst_top = m_buf)
	{
		ushort* pdst = (ushort*)(pdst_top + m_idx_byte);
		ushort* pTmnt_dst = pdst + m_rem_ui16 - EN_Margin_buf;

		// ptr_at_pos_text_len が null のときは、テキストセクションの処理外であると分かる
		ushort* ptr_at_pos_text_len = null;
		bool b_on_escaped = false;

		void CLOSE_TEXT_SEC_IF_OPEN()
		{
			if (ptr_at_pos_text_len != null)
			{
				*ptr_at_pos_text_len = (ushort)(pdst - ptr_at_pos_text_len - 1);
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
public unsafe void Simplify_Buf()
{
	try
	{
		fixed (byte* buf_top_byte = m_buf)
		{
			Text_section_compaction(buf_top_byte);

			// -------------------------------------------------------
			// その他のコンパクションをする場合は、この位置で行うこと

			// -------------------------------------------------------
			// FLG_no_BR_above の処理
			ushort* psrc = (ushort*)buf_top_byte;
			ushort* pTmnt_src = (ushort*)(buf_top_byte + m_idx_byte);
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

	}
	catch(Exception ex)
	{
		this.Wrt_ID(ID.ERR_on_Simplify);
		this.Wrt_PStr(ex.Message);
		throw;  // rethrow
	}
}

// ------------------------------------------------------------------------------------
public unsafe void Text_section_compaction(byte* buf_top_byte)
{
	ushort* psrc = (ushort*)buf_top_byte;
	ushort* pTmnt_src = (ushort*)(buf_top_byte + m_idx_byte);
	ushort* pdst = (ushort*)buf_top_byte;

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

			// テキストのコピー
			ushort* pdst_textbody_top = pdst;
			for (int i = *psrc++; i > 0; --i)
			{ *pdst++ = *psrc++; }
		}
		else  // text セクションでない場合の処理（End, ERR, BR, Div）
		{
			// text セクションが開いていた場合、それを閉じる
			if (ptr_at_pos_text_len != null)
			{
				//「装飾がない」行末の空白文字を削除
				if ((byte)*(ptr_at_pos_text_len - 1) == (byte)ID.Text)
				{
					for (long i = pdst - ptr_at_pos_text_len -1; i > 0; --i)  // text_len == 0 のときを考慮
					{
						ushort c = *(pdst - 1);
						if (c == Chr.SP || c == Chr.SP_ZEN) { pdst--; continue; }

						break;
					}
				}

				CLOSE_TEXT_SEC();
			}

			*pdst++ = chr;  // chr は ID.Txt を持たない
		}
	}  // while

	if (psrc > pTmnt_src)
	{ throw new Exception("!!! Text_section_compaction() : psrc > pTmnt_src となりました。"); }

	int chr_end_id = *(psrc - 1) & 0xff;
	if (chr_end_id != (int)ID.End)
	{ throw new Exception($"!!! Text_section_compaction() : chr_end = {chr_end_id.ToString()}"); }

	// -------------------------------------------------------
	// 終了処理 : m_idx_byte, m_rem_ui16 を再設定する
	m_idx_byte = (int)(((byte*)pdst) - buf_top_byte);
	m_rem_ui16 = (m_buf.Length - m_idx_byte) >> 1;
}


}  // Write_WS_Buffer
}  // namespace md_svr
