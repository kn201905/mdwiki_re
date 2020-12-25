'use strict';

const g_Read_Buf_to_Log = new function(){
	// 戻り値： 次の idx（利用するかどうかは、呼び出し側の都合で良い）
	this.Consume = (id) => {
		switch (id)
		{
		case ID_Directory_Names:
			const pcs_dir = g_Read_Buf.Get_param_cur();
			g_pnl_Log.Log('▶ ID: Directory Names / pcs : ' + pcs_dir);
			ShowFNames(pcs_dir);
			return true;

		case ID_File_Names:
			const pcs_file = g_Read_Buf.Get_param_cur();
			g_pnl_Log.Log('▶ ID: File Names / pcs : ' + pcs_file);
			ShowFNames(pcs_file);
			return true;

		case ID_End:
			g_pnl_Log.Log('▶ ID: End');
			return false;

		default:
			g_pnl_Log.Log(`▶ ID: 不明な ID です。 / ID = ${id}`);
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
