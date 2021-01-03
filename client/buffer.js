'use strict';

// 使い方
// const str = g_utf16_to_str.decode(typed_ary_uint16);
const g_utf16_to_str = new TextDecoder('utf-16');

const g_TStream_Reader = new function() {
	let m_ary_buffer = null;
	let m_u16_buf = null;

	let m_idx = 0;  // ui16 でカウントしていることに注意
	let m_len_uint16buf = 0;

	let m_param_cur = 0;
	let m_text_cur = null;

	this.SetArrayBuffer = (array_buffer) => {
		m_ary_buffer = array_buffer;
		m_u16_buf = new Uint16Array(array_buffer);
		m_idx = 0;
		m_len_uint16buf = m_u16_buf.length;
	};

	this.Reset_idx = () => { m_idx = 0;}

	this.Consume_NextID = () => {
		if (m_idx == m_len_uint16buf) { return null; }

		const val_uint16 = m_u16_buf[m_idx++];
		let val_id = val_uint16 & 0xff;
		m_param_cur = val_uint16 >>> 8;

		// テキストセクションの処理
		if (val_id & ID_Text) {
			if (m_param_cur > 0) {
				// 文字列長が 255 文字以下のとき
				if (m_len_uint16buf < m_idx + m_param_cur)
				{ throw new Error('!!! g_utf16_to_str.Read_ID() : バッファオーバーフロー'); }

				// 第２引数はバイト、第３引数は u16 で指定すること
				m_text_cur = g_utf16_to_str.decode(new Uint16Array(m_ary_buffer, m_idx * 2, m_param_cur));
				m_idx += m_param_cur;
			}
			else {
				// 文字列長が 256 文字以上のとき
				const pcs = m_u16_buf[m_idx];
				if (m_len_uint16buf < m_idx + 1 + pcs)
				{ throw new Error('!!! g_utf16_to_str.Read_ID() : バッファオーバーフロー'); }

				// 第２引数はバイト、第３引数は u16 で指定すること
				m_text_cur = g_utf16_to_str.decode(new Uint16Array(m_ary_buffer, (m_idx + 1) * 2, pcs));
				m_idx += 1 + pcs;
			}
		}
		else {
			m_text_cur = null;

			switch (val_id) {
			case ID_Num_int:
				m_param_cur = m_u16_buf[m_idx] + m_u16_buf[m_idx + 1] * 0x10000;
				m_idx += 2;
				break;
			}
		}

		return val_id;
	};

	this.Get_idx_cur = () => m_idx;
	this.Set_idx_cur = (idx) => { m_idx = idx; }
	this.Get_param_cur = () => m_param_cur;
	this.Get_text_cur = () => m_text_cur;
};

/////////////////////////////////////////////////////////////////////////////

const g_Write_Buf = new function() {
	const EN_BufSize = 2048;  // 送信用であるため、バッファのサイズは小さくて良い

	const m_ary_buffer = new ArrayBuffer(EN_BufSize);;
	const m_u16_full_buf = new Uint16Array(m_ary_buffer);
	let m_idx_u16 = 0;
	const m_len_uint16buf = m_u16_full_buf.length;

	this.Wrt_ID = (id_byte) => {
		if (m_idx_u16 >= m_len_uint16buf)
		{ throw new Error('!!! WriteBuffer.Wrt_ID() : バッファオーバーフロー'); }

		m_u16_full_buf[m_idx_u16++] = id_byte;
	};

	this.Wrt_ID_param = (id_byte, param_byte) => {
		if (m_idx_u16 >= m_len_uint16buf)
		{ throw new Error('!!! WriteBuffer.Wrt_ID_param() : バッファオーバーフロー'); }

		m_u16_full_buf[m_idx_u16++] = id_byte + (param_byte << 8);
	};
	
	this.Wrt_Num_int = (num_int) => {
		if (m_idx_u16 + 3 > m_len_uint16buf)
		{ throw new Error('!!! WriteBuffer.Wrt_Num_int() : バッファオーバーフロー'); }
		
		if (num_int < -0x80000000 || num_int > 0x7fffffff)
		{ throw new Error('!!! num_int の値が int の範囲を超えています。'); }
		
		if (num_int < 0) { num_int = 0x100000000 + num_int; }
		
		m_u16_full_buf[m_idx_u16] = ID_Num_int;
		m_u16_full_buf[m_idx_u16 + 1] = num_int & 0xffffffff;
		m_u16_full_buf[m_idx_u16 + 2] = num_int >>> 16;
		m_idx_u16 += 3;
	};

	// ID_Text の書き込みも行う
	this.Wrt_PStr = (src_str) => {
		let len_str = src_str.length;
		if (m_idx_u16 + len_str > m_len_uint16buf - 10) {  // 10 はマージン
			throw new Error('!!! WriteBuffer.Wrt_PStr() : バッファオーバーフロー');
		}
		if (len_str > 0xffff) {
			throw new Error('!!! WriteBuffer.Wrt_PStr() : 書き込み文字数が大きすぎます。');
		}

		m_u16_full_buf[m_idx_u16] = ID_Text;
		m_u16_full_buf[m_idx_u16 + 1] = len_str;
		m_idx_u16 += 2;

		let idx_src = 0;
		for (; len_str > 0; --len_str) {
			m_u16_full_buf[m_idx_u16++] = src_str.charCodeAt(idx_src++);
		}
	};

	this.Get_u16ary_Cur = () => {
		// 第２引数はバイト、第３引数は u16 で指定すること
		return new Uint16Array(m_ary_buffer, 0, m_idx_u16);
	};

	this.Flush = () => { m_idx_u16 = 0; }
};
