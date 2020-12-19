'use strict';

import * as Lexer from './Lexer_const.js'

let s_src = null;
let sa_token = [];
let sa_txt_blk = [];

////////////////////////////////////////////////////////////////////

const s_sprtr_finder = new function() {
	const RE_separator = /[#`]/g;

	let m_idx_uncheck = 0;	// セパレータの最初の文字か、平文の先頭を示す
	let m_idx_cur = 0;		// セパレータの先頭を表す
	let m_idx_tmnt = 0;

	let DBG_bRE_execable = false;  // RE_separator.lastIndex == m_idx_cur であるかどうか

	// ---------------------------------------------------
	this.Init = () => {
		RE_separator.lastIndex = 0;
		m_idx_uncheck = 0;
		m_idx_cur = 0;
		m_idx_tmnt = s_src.length;

		DBG_bRE_execable = true;
	};

	// ---------------------------------------------------
	// 戻り値: [ idx_macth, nextchr_match ]
	//!!! TODO: エスケープ文字の処理を付加すること
	this.Match_Next = () => {
		if (DBG_bRE_execable == false)
		{ throw new Error('!!! DBG_bRE_execable == false'); }
		DBG_bRE_execable = false;

		if (m_idx_cur == m_idx_tmnt) { return [null, null]; }

		const ary_re = RE_separator.exec(s_src);	
		if (ary_re == null) {
			m_idx_cur = m_idx_tmnt;
			return [null, null];
		}

		m_idx_cur = ary_re.index;
		if (m_idx_cur == m_idx_tmnt - 1) { return [m_idx_cur, null]; }

		return [m_idx_cur, s_src[m_idx_cur + 1]];
	};

	// ---------------------------------------------------
	this.Check = (len_sep, id_token) => {
		if (m_idx_cur != m_idx_uncheck) {
			// テキストブロックの生成
			//!!! TODO: エスケープ文字の処理
			sa_token.push(Lexer.LEX_text);
			sa_txt_blk.push(s_src.substring(m_idx_uncheck, m_idx_cur));
		}
		sa_token.push(id_token);

		m_idx_cur += len_sep;
		m_idx_uncheck = m_idx_cur;
		RE_separator.lastIndex = m_idx_cur;
		DBG_bRE_execable = true;
	};

	// ---------------------------------------------------
	this.Uncheck = (len_skip) => {
		m_idx_cur += len_skip;
		RE_separator.lastIndex = m_idx_cur;
		DBG_bRE_execable = true;
	};

	// ---------------------------------------------------
	this.Finish = () => {
		if (m_idx_uncheck != m_idx_tmnt) {
			// テキストブロックの生成
			//!!! TODO: エスケープ文字の処理
			sa_token.push(Lexer.LEX_text);
			sa_txt_blk.push(s_src.substring(m_idx_uncheck, m_idx_tmnt));
		}

		m_idx_uncheck = m_idx_tmnt;
		m_idx_cur = m_idx_tmnt;
		RE_separator.lastIndex = m_idx_tmnt;
		DBG_bRE_execable = true;

		sa_token.push(Lexer.LEX_end);
	};

	// ---------------------------------------------------
	// 別のロジックで解析する場合（デバッグする場合も利用）
	this.SkipTo = (idx_after_skip) => {
		if (idx_after_skip > m_idx_tmnt)
		{ throw new Error('!!! idx_after_skip > m_idx_tmnt'); }

		m_idx_uncheck = idx_after_skip;
		m_idx_cur = idx_after_skip;
		RE_separator.lastIndex = idx_after_skip;
		DBG_bRE_execable = true;
	};
};

////////////////////////////////////////////////////////////////////

export const RET_Lexer_OK = 1;
export const RET_Lexer_Err = 0;

export const g_lexer = new function() {
	const EN_continue = 1;
	const EN_break = 0;
	const EN_err = -1;

	let m_idx_tmnt = 0;

	// ---------------------------------------------------
	this.Run = (src) => {
		s_src = src
			.replace(/\r\n/g, '\n')
//			.replace(/^ +/gm, '')
			.replace(/\t/g, '   ')
//			.replace(/\u00a0/g, ' ')
//			.replace(/\u2424/g, '\n');

		s_sprtr_finder.Init();
		m_idx_tmnt = s_src.length;

		while (true) {
			const [idx_match, nextchr_match] = s_sprtr_finder.Match_Next();
			if (idx_match == null) { break; }  // src の終端に到達

			let result = EN_err;
			switch (s_src[idx_match]) {
			case '#':
				result = Case_head(idx_match, nextchr_match);
				break;

			case '`':
				result = Case_code(idx_match, nextchr_match);
				break;
			}

			// ----------------------------------------
			if (result == EN_continue) { continue; }
			if (result == EN_break) { break; }

			console.log('!!! ERROR');

			sa_token.push(Lexer.LEX_ERR);
			Lexer.DBG_ShowToken(sa_token);
			console.log(sa_txt_blk);

			return RET_Lexer_Err;
		}  // WHILE_Search_Separtor

		s_sprtr_finder.Finish();

		Lexer.DBG_ShowToken(sa_token);
		console.log(sa_txt_blk);

		return RET_Lexer_OK;
	};

	// ---------------------------------------------------
	// 戻り値: EN_continue, EN_break
	const RE_head = /#+/g

	const Case_head = (idx_match, nextchr_match) => {
		// head_num の決定
		let head_num = 1;
		switch (nextchr_match) {
		case null:
			return false;

		case '#':
			RE_head.lastIndex = idx_match + 1;
			let ary_re_haed = RE_head.exec(s_src);
			head_num = ary_re_haed[0].length + 1;  // head_num >= 2
		}

		if (idx_match + head_num == m_idx_tmnt) { return EN_break; }
		if (s_src[idx_match + head_num] != ' ') {
			s_sprtr_finder.Uncheck(head_num);
			return EN_continue;
		}
		
//		console.log('head_num -> ' + head_num);
		// +1 は、スペースの分
		s_sprtr_finder.Check(head_num + 1, Lexer.LEX_head_1 + head_num - 1);
		return EN_continue;
	};

	// ---------------------------------------------------
	// 戻り値: EN_continue, EN_break, EN_err
	const RE_code_blk = /```\n/g
	const RE_code_inline = /`/g

	const Case_code = (idx_match, nextchr_match) => {
		let idx_top_code = 0;
		let idx_tmnt_code = 0;

		if (nextchr_match == '`') {
			// LEX_code_blk として処理
//			console.log('--- LEX_code_blk');
			if (s_src[idx_match + 2] != '`') { return EN_err; }
			if (s_src[idx_match + 3] != '\n') { return EN_err; }

			RE_code_blk.lastIndex = idx_match + 4;
			const ary_re = RE_code_blk.exec(s_src);
			if (ary_re == null) { return EN_err; }

			s_sprtr_finder.Check(4, Lexer.LEX_code_blk);

			idx_top_code = idx_match + 4;
			idx_tmnt_code = ary_re.index;
			s_sprtr_finder.SkipTo(idx_tmnt_code + 4);

		} else {
//			console.log('--- LEX_code_inline');
			// LEX_code_inline として処理
			RE_code_inline.lastIndex = idx_match + 2;
			const ary_re = RE_code_inline.exec(s_src);
			if (ary_re == null) { return EN_err; }
			
			s_sprtr_finder.Check(1, Lexer.LEX_code_inline);

			idx_top_code = idx_match + 1;
			idx_tmnt_code = ary_re.index;
			s_sprtr_finder.SkipTo(idx_tmnt_code + 1);
		}

		//!!! TODO: 独自コードブロックの処理
		// 今は簡易処理にしておく

		sa_token.push(Lexer.LEX_text);
		sa_txt_blk.push(s_src.substring(idx_top_code, idx_tmnt_code));

		sa_token.push(Lexer.LEX_code_end);

		return EN_continue;
	};
};

