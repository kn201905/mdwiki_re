'use strict';

const g_DomTree = new function() {
	let m_e_div_dst = null;

	let m_cur_div;
	let m_Quote_div;

	let mb_CodeBlk;
	let mb_Quote;

	let mb_idx_DivBlk;

	// div_dst は、g_e_DomTreeArea を想定している
	// buf_reader は、g_Read_Buf を想定している
	// ID_Lexed_MD を検知した後、コールされることを想定している
	this.BuildTree = (e_div_dst) => {
		e_div_dst.Remove_All();
		m_e_div_dst = e_div_dst;

		m_cur_div = null;
		m_Quote_div = null;

		mb_CodeBlk = false;
		mb_Quote = false;

		mb_idx_DivBlk = 0;

		g_Read_Buf.Reset_idx();
		while (true) {
			const id = g_Read_Buf.Consume_NextID();
			let e_err_div;  // エラー用の Div として利用する

			if (id & ID_Text)
			{
				m_cur_div.appendChild(document.createTextNode(g_Read_Buf.Get_text_cur()));
				continue;
			}

			if (id & ID_Div)
			{
				mb_idx_DivBlk++;
				if (Build_DivBlk(id) == true) {
					continue;
				}
				
				console.log("+++ ERR -> idx_DivBlk : " + mb_idx_DivBlk.toString());
				return;  // ここで BuildTree() を打ち切る
			}

			switch (id)
			{
			case ID_Lexed_MD:
				// TODO: ここでエラーの検出を行う
				continue;

			case ID_End:
				return;  // BuildTree() の正常終了

			case ID_BR:
				m_cur_div.Add_BR();
				continue;

			// -------------------------------------------------------------------
			case ID_ERR_OVERFLOW:
				e_err_div = m_e_div_dst.Add_DivTxt("!!! ERR_OVERFLOW を検出しました。DomTree の生成を中断します。");
				break;

			case ID_ERR_Report:
				e_err_div = m_e_div_dst.Add_DivTxt("!!! ERR_Report を検出しました。");
				break;

			case ID_ERR_on_Simplify:
				e_err_div = m_e_div_dst.Add_DivTxt("!!! ERR_on_Simplify を検出しました。");
				break;
			}

			// -------------------------------------------------------------------
			// エラー処理
			// TODO: e_err_div を、エラー用にすること
		}
	};

	const Build_DivBlk = (id) => {
		switch(id)
		{
		case ID_Div:
			m_cur_div = m_e_div_dst.Add_Div();
//			m_cur_div.classList.add('Div');
			break;

		case ID_Div_Head:
			// TODO: Head にすること
			m_cur_div = m_e_div_dst.Add_Div();
			m_cur_div.classList.add('Head');
			const heed_num = g_Read_Buf.Get_param_cur();
			switch (heed_num)
			{
			case 1:
				m_cur_div.style.fontSize = '2rem';
				break;
			
			case 2:
				m_cur_div.style.fontSize = '1.5rem';
				break;
			}
			break;

		case ID_Div_Code:
			mb_CodeBlk = !mb_CodeBlk;
			// mb_CodeBlk == false の場合は、処理が不要なはず、、、
			if (mb_CodeBlk == true)
			{
				// TODO: CodeBlk にすること
				m_cur_div = m_e_div_dst.Add_Div();
				m_cur_div.classList.add('CodeBlk');
			}
			break;

		case ID_Div_Quote:
			mb_Quote = !mb_Quote;
			if (mb_Quote == true) {
				// Quote ブロックに入る
				const e_QuoteBlk = m_e_div_dst.Add_FlexStg();
				e_QuoteBlk.classList.add('QuoteBlk');

				const e_left_stg = e_QuoteBlk.Add_Div();
				e_left_stg.classList.add('QuoteLine');

				// m_e_div_dst は right_stg となる
				const e_right_stg = e_QuoteBlk.Add_Div();
				m_e_div_dst = e_right_stg;  // m_e_div_dst をリカーシブに利用
				m_cur_div = null;  // バグ検出用
			}
			else {
				// Quote ブロックから出る
				const e_QuoteBlk = m_e_div_dst.parentNode;
				m_e_div_dst = e_QuoteBlk.parentNode;
				m_cur_div = null;  // バグ検出用
			}
			break;

		default:
			// TODO: エラー用にすること
			const e_err_div = m_e_div_dst.Add_DivTxt("!!! 不明な ID を検出しました。ここで DomTree の生成を中断します。 id : " + id.toString());
			return false;
		} // switch

		// Div の　BR_above 処理
		if (m_cur_div && (g_Read_Buf.Get_param_cur() & Param_no_BR_above) == 0) {
			m_cur_div.style.marginTop = '1rem';
		}

		return true;
	}
};
