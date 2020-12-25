'use strict';

// 使い方
// const str = g_utf16_to_str.decode(typed_ary_uint16);
const g_utf16_to_str = new TextDecoder('utf-16');

const g_Read_Buf = new function() {
	let m_ary_buffer = null;
	let m_u16_buf = null;
	let m_idx = 0;
	let m_len_uint16buf = 0;

	let m_param_cur = 0;
	let m_text_cur = null;

	this.SetArrayBuffer = (array_buffer) => {
		m_ary_buffer = array_buffer;
		m_u16_buf = new Uint16Array(array_buffer);
		m_idx = 0;
		m_len_uint16buf = m_u16_buf.length;
	};

	this.Read_ID = () => {
		if (m_idx == m_len_uint16buf) { return null; }

		const val_uint16 = m_u16_buf[m_idx++];
		let val_id = val_uint16 & 0xff;
		m_param_cur = val_uint16 >>> 8;

		if (val_id == ID_Text) {
			const pcs = m_u16_buf[m_idx];
			if (m_idx + 1 + pcs > m_len_uint16buf)
			{ throw new Error('!!! g_utf16_to_str.Read_ID() : m_idx + pcs > m_len_uint16buf'); }

			// 第２引数はバイト、第３引数は u16 で指定すること
			m_text_cur = g_utf16_to_str.decode(new Uint16Array(m_ary_buffer, (m_idx + 1) * 2, pcs));
			m_idx += 1 + pcs;
		}
		else {
			m_text_cur = null;
		}

		return val_id;
	};

	this.Get_idx_cur = () => m_idx;
	this.Set_idx_cur = (idx) => { m_idx = idx; }
	this.Get_param_cur = () => m_param_cur;
	this.Get_text_cur = () => m_text_cur;

	// ----------------------------------------------------------------
	let m_peek_param_cur = 0;
	let m_peek_text_cur = null

	this.Peek_ID = (peek_idx) => {
		if (peek_idx == m_len_uint16buf) { return [null, null]; }

		const val_uint16 = m_u16_buf[peek_idx++];
		let val_id = val_uint16 & 0xff;
		m_peek_param_cur = val_uint16 >>> 8;

		if (val_id == ID_Text) {
			const pcs = m_u16_buf[peek_idx];
			if (peek_idx + 1 + pcs > m_len_uint16buf)
			{ throw new Error('!!! g_utf16_to_str.Read_ID() : peek_idx + pcs > m_len_uint16buf'); }

			// 第２引数はバイト、第３引数は u16 で指定すること
			m_peek_text_cur
				= g_utf16_to_str.decode(new Uint16Array(m_ary_buffer, (peek_idx + 1) * 2, pcs));
			peek_idx += 1 + pcs;
		}
		else {
			m_text_cur = null;
		}

		return [peek_idx, val_id];
	};

	this.Get_peek_param = () => m_peek_param_cur;
	this.Get_peek_text = () => m_peek_text_cur;
};

/////////////////////////////////////////////////////////////////////////////

const g_Write_Buf = new function() {
	const EN_BufSize = 2048;  // 送信用であるため、バッファのサイズは小さくて良い

	const m_ary_buffer = new ArrayBuffer(EN_BufSize);;
	const m_u16_full_buf = new Uint16Array(m_ary_buffer);
	let m_idx = 0;
	const m_len_uint16buf = m_u16_full_buf.length;

	this.Wrt_ID = (id_byte) => {
		m_u16_full_buf[m_idx++] = id_byte;
	};

	this.Wrt_ID_param = (id_byte, param_byte) => {
		m_u16_full_buf[m_idx++] = id_byte + (param_byte << 8);
	};

	this.Wrt_PStr = (src_str) => {
		const len_str = src_str.length;
		if (m_idx + len_str > m_len_uint16buf - 10) {  // 10 はマージン
			throw new Error('!!! WriteBuffer.Wrt_PStr() : バッファサイズが不足しています。');
		}
		if (len_str > 0xffff) {
			throw new Error('!!! WriteBuffer.Wrt_PStr() : 書き込み文字数が大きすぎます。');
		}

		m_u16_full_buf[m_idx] = ID_Text;
		m_u16_full_buf[m_idx + 2] = len_str;
		m_idx += 2;

		let idx_src = 0;
		for (; len_str > 0; --len_str) {
			m_u16_full_buf[m_idx++] = src_str.charCodeAt(idx_src++);
		}
	};

	this.Get_u16ary_Cur = () => {
		// 第２引数はバイト、第３引数は u16 で指定すること
		return new Uint16Array(m_ary_buffer, 0, m_idx);
	};

	this.Flush = () => { m_idx = 0; }
};
