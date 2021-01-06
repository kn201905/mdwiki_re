'use strict';

const g_DomTree = new function() {
	let m_cur_div;

	let mb_QuoteBlk;
	let mb_idx_DivBlk;

	let e_oframe_div_creating;
	
	// ID_Lexed_MD を検知した後の、g_TStream_Reader を用いて dom_tree を生成する
	this.BuildTree = () => {
		e_oframe_div_creating = document.createElement('div');;
		
		e_oframe_div_creating.classList.add('DomTree');
		{
			const e_top_dom_tree = e_oframe_div_creating.Add_Div();
			e_top_dom_tree.classList.add('HLine');
			e_top_dom_tree.style.marginBottom = '1rem';
		}

		m_cur_div = null;
		mb_QuoteBlk = false;

		mb_idx_DivBlk = 0;

		while (true) {
			const id = g_TStream_Reader.Consume_NextID();
			let e_err_div;  // エラー用の Div として利用する

			if (id & ID_Text)
			{
				const e_new = m_cur_div.Add_Element(null);
				e_new.textContent = g_TStream_Reader.Get_text_cur();

				if (id & TxtFLG_Bold) { e_new.style.fontWeight = 'bold'; }
				if (id & TxtFLG_Cancel) { e_new.style.textDecoration = 'line-through'; }
				if (id & TxtFLG_Under) { e_new.classList.add('inline_Underline'); }
				if (id & TxtFLG_Code) { e_new.classList.add('inline_Code'); }
				continue;
			}

			if (id & ID_Div)
			{
				mb_idx_DivBlk++;
				if (Build_DivBlk(id) == true) {
					continue;
				}
				
				console.log("+++ ERR -> idx_DivBlk : " + mb_idx_DivBlk.toString());
				return e_oframe_div_creating;  // ここで BuildTree() を打ち切る
			}

			switch (id)
			{
			case ID_Lexed_MD:
				// TODO: ここでエラーの検出を行う
				continue;

			case ID_End:
				return e_oframe_div_creating;  // BuildTree() の正常終了

			case ID_BR:
				m_cur_div.Add_BR();
				continue;

			case ID_HLine:
				const e_HLine = e_oframe_div_creating.Add_Div();
				e_HLine.classList.add('HLine');
				m_cur_div = null;  // エラー顕在化
				continue;

			// -------------------------------------------------------------------
			case ID_ERR_OVERFLOW:
				e_err_div = e_oframe_div_creating.Add_DivTxt("!!! ERR_OVERFLOW : DomTree の生成を中断します。");
				break;

			case ID_ERR_Report:
				e_err_div = e_oframe_div_creating.Add_DivTxt("!!! ERR_Report : DomTree の生成を中断します。");
				break;

			case ID_ERR_on_Simplify:
				e_err_div = e_oframe_div_creating.Add_DivTxt("!!! ERR_on_Simplify : DomTree の生成を中断します。");
				break;
				
			default:
				e_err_div = e_oframe_div_creating.Add_DivTxt("!!! 不明な ID を検出 : id -> " + id);
				e_err_div.style.fontSize = '2rem';
				e_err_div.style.color = '#F00';
				return e_oframe_div_creating;  // id が不明なため、dom_tree の生成を abort する。
			}

			// -------------------------------------------------------------------
			// エラー表示処理
			console.log(e_err_div);
			
			e_err_div.style.fontSize = '2rem';
			e_err_div.style.color = '#F00';
			
			// QuoteBlk に入っていたら、それを抜け出す
			if (mb_QuoteBlk == true) {
				const e_QuoteBlk = e_oframe_div_creating.parentNode;
				e_oframe_div_creating = e_QuoteBlk.parentNode;
				m_cur_div = null;  // バグ検出用
			}
		} // while
	}; // BuildTree
	
	// -------------------------------------------------------------------------
	const Build_DivBlk = (id) => {
		switch(id)
		{
		case ID_Div:
			m_cur_div = e_oframe_div_creating.Add_Div();
//			m_cur_div.classList.add('Div');
			break;

		case ID_Div_Head:
			m_cur_div = e_oframe_div_creating.Add_Div();
			m_cur_div.classList.add('Head');
			const heed_num = g_TStream_Reader.Get_param_cur();
			switch (heed_num)
			{
			case 1:
				m_cur_div.style.fontSize = '1.8rem';
//				const e_h1_underline = e_oframe_div_creating.Add_Div();
//				e_h1_underline.classList.add('h1_underline');
				break;
			
			case 2:
				m_cur_div.style.fontSize = '1.5rem';
				break;
			}
			break;

		case ID_Div_Code:
			m_cur_div = e_oframe_div_creating.Add_Div();
			m_cur_div.classList.add('CodeBlk');
			break;

		case ID_Div_Quote:
			mb_QuoteBlk = !mb_QuoteBlk;
			if (mb_QuoteBlk == true) {
				// Quote ブロックに入る
				const e_QuoteBlk = e_oframe_div_creating.Add_FlexStg();
				e_QuoteBlk.classList.add('QuoteBlk');

				const e_left_stg = e_QuoteBlk.Add_Div();
				e_left_stg.classList.add('Quote_leftLine');

				// e_oframe_div_creating は right_stg となる
				const e_right_stg = e_QuoteBlk.Add_Div();
				e_oframe_div_creating = e_right_stg;  // e_oframe_div_creating をリカーシブに利用
				m_cur_div = null;  // バグ検出用
			}
			else {
				// Quote ブロックから出る
				const e_QuoteBlk = e_oframe_div_creating.parentNode;
				e_oframe_div_creating = e_QuoteBlk.parentNode;
				m_cur_div = null;  // バグ検出用
			}
			break;

		case ID_Div_Bullet:
			const e_BulletBlk = e_oframe_div_creating.Add_Div();
			e_BulletBlk.style.display = 'flex';
			e_BulletBlk.style.marginTop = '1rem';
			
			const e_left_stg = e_BulletBlk.Add_Div();
			e_left_stg.textContent = "\xa0•\xa0";

			m_cur_div = e_BulletBlk.Add_Div();  // right_stg
			return true;  // break すると、Div の　BR_above 処理が入ってしまうため注意

		default:
			// TODO: エラー用にすること
			const e_err_div = e_oframe_div_creating.Add_DivTxt("!!! 不明な ID を検出。ここで DomTree の生成を中断します。 id : " + id.toString());
			return false;
		} // switch

		// Div の　BR_above 処理
		if (m_cur_div && g_TStream_Reader.FLG_no_BR_above() == 0) {
			m_cur_div.style.marginTop = '1rem';
		}

		return true;
	} // Build_DivBlk
};
