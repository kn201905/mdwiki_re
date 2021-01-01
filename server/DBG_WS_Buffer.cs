using System.Text;
using System.Windows.Forms;
using System.Drawing;  // Color を利用するため

namespace md_svr
{

static class DBG_WS_Buffer
{
static readonly Color EN_Color_bkgd_on_Err = Color.FromArgb(255, 200, 200);
public static UnicodeEncoding ms_utf16_encoding = new UnicodeEncoding(false, true, true);
static RichTextBox ms_rbox;

static int ms_DivBlk_idx;
//static bool msb_Div_Code = false;
static bool msb_Div_Quote = false;

// ------------------------------------------------------------------------------------
static string ms_str_txt_flags = "[text /     ] ";

public static unsafe void Show_WS_buf(RichTextBox rbox, byte[] ws_buf, int pTmnt_idx_byte)
{
	ms_rbox = rbox;
	ms_DivBlk_idx = 1;

	fixed (byte* psrc_top = ws_buf)
	fixed (char* pstr_txt_flags = ms_str_txt_flags)
	{
		ushort* psrc = (ushort*)psrc_top;
		ushort* pTmnt_src = psrc + (pTmnt_idx_byte / 2);

		while (psrc < pTmnt_src)
		{
			ushort chr = *psrc++;
			ID id = (ID)chr;
			byte param = (byte)(chr >> 8);

			if (id.IsDiv())
			{
				Show_Div_detail(id, param);
				continue;
			}

			if (id.IsText())
			{
				int text_len, idx_src_byte;
				if (param > 0)
				{
					text_len = param;
					idx_src_byte = (int)(((byte*)psrc) - psrc_top);
					psrc += text_len;
				}
				else
				{
					text_len = *psrc;
					idx_src_byte = (int)(((byte*)(psrc + 1)) - psrc_top);
					psrc += text_len + 1;
				}

				// ms_str_txt_flags の生成
				if (id.HasFlag(ID.Txt_Bold)) { *(pstr_txt_flags + 8) = 'B'; }
				else { *(pstr_txt_flags + 8) = ' '; }

				if (id.HasFlag(ID.Txt_Cancel)) { *(pstr_txt_flags + 9) = 'C'; }
				else { *(pstr_txt_flags + 9) = ' '; }

				if (id.HasFlag(ID.Txt_Under)) { *(pstr_txt_flags + 10) = 'U'; }
				else { *(pstr_txt_flags + 10) = ' '; }

				if (id.HasFlag(ID.Txt_Code)) { *(pstr_txt_flags + 11) = 'D'; }
				else { *(pstr_txt_flags + 11) = ' '; }

				// 第２引数、第３引数ともに、バイト数で表していることに注意
				string s = ms_utf16_encoding.GetString(ws_buf, idx_src_byte, text_len << 1);
				rbox.AppendText(ms_str_txt_flags + s +"\r\n");
				continue;
			}

			string err_msg;
			switch (id)
			{
			// ----------------------------------------------------------
			case ID.Directory_Names:
				rbox.AppendText($"[Directory_Names] -> {param.ToString()}\r\n");
				continue;

			case ID.File_Names:
				rbox.AppendText($"[File_Names] -> {param.ToString()}\r\n");
				continue;

			case ID.End:
				rbox.AppendText("[End]\r\n");
				continue;

			case ID.BR:
				rbox.AppendText("[BR]\r\n");
				continue;

			case ID.HLine:
				rbox.AppendText("[HLine]\r\n");
				continue;

			
			// ----------------------------------------------------------
			case ID.Lexed_MD:
				if (param == Param.EN_Succeeded)
				{ rbox.AppendText("[Lexed_MD] ▶ succeeded\r\n"); }
				else
				{ rbox.AppendText("[Lexed_MD] ▶ failed\r\n"); }
				continue;

			case ID.ERR_OVERFLOW:
				err_msg = "[ERR_OVERFLOW]\r\n";
				break;

			case ID.ERR_Report:
				err_msg = "[ERR_Report]\r\n";
				break;

			case ID.ERR_on_Simplify:
				err_msg = "[ERR_on_Simplify]\r\n";
				break;
			
			default:
				err_msg = $"[？？？] -> {((int)id).ToString()}\r\n";
				break;
			} // switch

			// ----------------------------------------------------------
			// エラーの表示
			rbox.SelectionBackColor = EN_Color_bkgd_on_Err;
			rbox.AppendText(err_msg + "\r\n");
			rbox.SelectionBackColor = Color.White;
		} // while

		if (psrc == pTmnt_src)
		{ rbox.AppendText("--- 正常解析終了\r\n"); }
		else
		{ rbox.AppendText("!!! psrc > pTmnt_src\r\n"); }
	} // fixed
}

// ------------------------------------------------------------------------------------
static void Show_Div_detail(ID id, byte param)
{
	ms_rbox.SelectionColor = Color.FromArgb(255, 100, 0);
	ms_rbox.AppendText(ms_DivBlk_idx.ToString("D4"));
	ms_DivBlk_idx++;

	switch (id)
	{
	case ID.Div:
		ms_rbox.AppendText(" [Div]\r\n");
		break;

	case ID.Div_Head:
		ms_rbox.AppendText($" [Div_Head] ▶ {param.ToString()}\r\n");
		break;

	case ID.Div_Code:
		ms_rbox.AppendText(" [Div_Code]\r\n");
		break;
/*
		msb_Div_Code = !msb_Div_Code;
		if (msb_Div_Code == true)
		{ ms_rbox.AppendText(" [Div_Code] ▶ start\r\n"); }
		else
		{ ms_rbox.AppendText(" [Div_Code] ▶ end\r\n"); }
		break;
*/
	case ID.Div_Quote:
		msb_Div_Quote = !msb_Div_Quote;
		if (msb_Div_Quote == true)
		{ ms_rbox.AppendText(" [Div_Quote] ▶ start\r\n"); }
		else
		{ ms_rbox.AppendText(" [Div_Quote] ▶ end\r\n"); }
		break;

	case ID.Div_Bullet:
		ms_rbox.AppendText(" [Div_Bullet]\r\n");
		break;

	default:
		ms_rbox.SelectionBackColor = EN_Color_bkgd_on_Err;
		ms_rbox.AppendText($" [？？？] ▶ {((int)id).ToString()}\r\n");
		ms_rbox.SelectionBackColor = Color.White;
		break;
	}

	ms_rbox.SelectionColor = Color.Black;
}


}  // class DBG_WS_Buffer
}  // namespace md_svr
