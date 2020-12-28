'use strict';

const g_Read_Buf_to_Log = new function() {
	let mb_CodeBlk = false;
	let mb_CodeQuote = false;
	let m_str;

	// 戻り値： 次の idx（利用するかどうかは、呼び出し側の都合で良い）
	this.Show = (id) => {
		switch (id)
		{
		case ID_Directory_Names:
			const pcs_dir = g_Read_Buf.Get_param_cur();
			g_pnl_Log.Log('▶ Directory Names / pcs : ' + pcs_dir);
			ShowFNames(pcs_dir);
			return true;

		case ID_File_Names:
			const pcs_file = g_Read_Buf.Get_param_cur();
			g_pnl_Log.Log('▶ File Names / pcs : ' + pcs_file);
			ShowFNames(pcs_file);
			return true;

		case ID_End:
			g_pnl_Log.Log('▶ End');
			return false;

		case ID_Text:
			g_pnl_Log.Log(`▶ Text : ${g_Read_Buf.Get_text_cur()}`);
			return true;

		// -----------------------------------------------------------------------
		case ID_Lexed_MD:
			mb_CodeBlk = false;
			mb_CodeQuote = false;

			switch (g_Read_Buf.Get_param_cur())
			{
			case Param_Succeeded: m_str = 'succeeded'; break;
			case Param_Failed: m_str = 'failed'; break;
			default: m_str = '？？？';
			}
			g_pnl_Log.Log('▶ Lexed -> ' + m_str);
			return true;

		case ID_ERR_OVERFLOW:
			g_pnl_Log.Log('▶ !!! ERR_OVERFLOW');
			return false;

		case ID_ERR_Report:
			g_pnl_Log.Log('▶ !!! ERR_Report');
			return true;

		case ID_ERR_on_Simplify:
			g_pnl_Log.Log('▶ !!! ERR_on_Simplify');
			return true;


		case ID_BR:
			g_pnl_Log.Log('▶ BR');
			return true;

		// -----------------------------------------------------------------------
		case ID_Div:
			g_pnl_Log.Log('▶ Div');
			return true;

		case ID_Div_Head:
			g_pnl_Log.Log(`▶ Head -> ${g_Read_Buf.Get_param_cur()}`);
			return true;

		case ID_Div_Code:
			mb_CodeBlk = !mb_CodeBlk;
			if (mb_CodeBlk == true) { m_str = 'start'; }
			else { m_str = 'end'; }
			g_pnl_Log.Log('▶ Code -> ' + m_str);
			return true;

		case ID_Div_Quote:
			mb_CodeQuote = !mb_CodeQuote;
			if (mb_CodeQuote == true) { m_str = 'start'; }
			else { m_str = 'end'; }
			g_pnl_Log.Log('▶ Quote -> ' + m_str);
			return true;

		// -----------------------------------------------------------------------
		default:
			g_pnl_Log.Log(`▶ 不明な ID です。 / ID = ${id}`);
			return false;
		}
	};

	const ShowFNames = (pcs) => {
		let peek_idx = g_Read_Buf.Get_idx_cur();
		let peek_id;
		let str_fnames = null;

		for (; pcs > 0; --pcs) {
			const id = g_Read_Buf.Read_ID();
			if (id != ID_Text)
			{ throw new Error(`!!! FNames において、id != ID_Text となりました。/ ID = : ${id}`); }

			if (str_fnames == null) {
				str_fnames = g_Read_Buf.Get_text_cur();
			}
			else {
				str_fnames += ' / ' + g_Read_Buf.Get_text_cur();
			}
		}

		g_pnl_Log.Log(str_fnames);
	};
	
	const ShowFNames_Peek = (pcs) => {
		let peek_idx = g_Read_Buf.Get_idx_cur();
		let peek_id;
		let str_fnames = null;

		for (; pcs > 0; --pcs) {
			[peek_idx, peek_id] = g_Read_Buf.Peek_ID(peek_idx);
			if (peek_id != ID_Text)
			{ throw new Error(`!!! FNames において、id != ID_Text となりました。/ ID = : ${peek_id}`); }

			if (str_fnames == null) {
				str_fnames = g_Read_Buf.Get_peek_text();
			}
			else {
				str_fnames += ' / ' + g_Read_Buf.Get_peek_text();
			}
		}

		g_pnl_Log.Log(str_fnames);
		return peek_idx;
	};
};
