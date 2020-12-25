using System;
using System.Text;

namespace md_svr
{
	class Read_Buffer
	{
		public static UnicodeEncoding ms_utf16_encoding;

		byte[] m_buf;

		int m_idx_byte = 0;
		int m_rem_ui16 = 0;  // �R���X�g���N�g���ɂ́A�ǂݎ��o�b�t�@�� 0 �ł���͂�

		byte m_param_cur = 0;
		string m_text_cur = null;

		// ------------------------------------------------------------------------------------
		public Read_Buffer(byte[] buf)
		{
			m_buf = buf;
		}

		// ------------------------------------------------------------------------------------
		// �ǂݎ��o�b�t�@����ɂȂ��Ă���ꍇ�́A0 ���Ԃ����
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
				{ throw new Exception("Read_Buffer.Read_ID() : m_rem_ui16 == 0"); }
				
				int len_txt = m_buf[m_idx_byte] + (m_buf[m_idx_byte + 1] << 8);
				if (len_txt >= m_rem_ui16)
				{ throw new Exception("Read_Buffer.Read_ID() : len_txt >= m_rem_ui16"); }

				// ��Q�����A��R�����Ƃ��ɁA�o�C�g���ŕ\���Ă��邱�Ƃɒ���
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

		// WebSocket �œǂݍ��܂ꂽ�o�C�g����ݒ肷�邱�Ƃ��l���Ă���
		public void Renew(int len_bytes)
		{
			if ((len_bytes & 1) != 0)
			{ throw new Exception("Read_Buffer.Renew() : len_bytes ����ł��B"); }

			m_idx_byte = 0;
			m_rem_ui16 = len_bytes >> 1;
		}
	}

	/////////////////////////////////////////////////////////////////////////////////////

	class Write_Buffer
	{
		byte[] m_buf;

		int m_idx_byte = 0;
		int m_rem_ui16;

		// ------------------------------------------------------------------------------------
		public Write_Buffer(byte[] buf)
		{
			m_buf = buf;
			m_rem_ui16 = buf.Length >> 1;
		}

		// ------------------------------------------------------------------------------------
		public void Wrt_ID(byte id)
		{
			if (m_rem_ui16 <= 0)
			{ throw new Exception("Write_Buffer.Wrt_ID() : m_rem_ui16 <= 0"); }

			m_buf[m_idx_byte] = id;
			m_buf[m_idx_byte + 1] = 0;
			m_idx_byte += 2;
			m_rem_ui16--;
		}

		// ------------------------------------------------------------------------------------
		public void Wrt_ID_param(byte id, byte param)
		{
			if (m_rem_ui16 <= 0)
			{ throw new Exception("Write_Buffer.Wrt_ID_param() : m_rem_ui16 <= 0"); }

			m_buf[m_idx_byte] = id;
			m_buf[m_idx_byte + 1] = param;
			m_idx_byte += 2;
			m_rem_ui16--;
		}

		// ------------------------------------------------------------------------------------
		public void Wrt_PStr(string src_str)
		{
			int len_str = src_str.Length;
			m_rem_ui16 -= len_str + 2;  // +2 : ID_Text �� ������
			if (m_rem_ui16 < 0)
			{ throw new Exception("Write_Buffer.Wrt_PStr() : m_rem_ui16 < 0"); }
			
			unsafe 
			{
				fixed (char* psrc_top = src_str)
				fixed (byte* pdst_top = m_buf)
				{
					char* psrc = psrc_top;
					char* pdst = (char*)(pdst_top + m_idx_byte);

					*pdst = (char)ID.Text;
					*(pdst + 1) = (char)len_str;

					pdst += 2;
					for (; len_str > 0; --len_str)
					{ *pdst++ = *psrc++; }

					m_idx_byte = (int)(((byte*)pdst) - pdst_top);
				}
			}
		}

		// ------------------------------------------------------------------------------------
		// �Ō�́u/�v�ȍ~�݂̂��L�^�����
		public void Wrt_PFName(string src_str)
		{
			int len_str = src_str.Length;
			if (m_rem_ui16 < len_str + 2)  // +2 : ID_Text �� ������
			{ throw new Exception("Write_Buffer.Wrt_PStr() : m_rem_ui16 < len_str + 2"); }
			
			unsafe 
			{
				fixed (char* psrc_top = src_str)
				fixed (byte* pdst_top = m_buf)
				{
					char* psrc_tmnt = psrc_top + len_str;
					char* pdst = (char*)(pdst_top + m_idx_byte);

					// �܂��A�Ō�̃Z�p���[�^��T��
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
		public void Flush()
		{
			m_idx_byte = 0;
			m_rem_ui16 = m_buf.Length >> 1;
		}

		// ------------------------------------------------------------------------------------
		// Text �ł́AID, param �̏������݂���ɂ����
		public int Get_idx_byte_cur() => m_idx_byte;

		public void Skip_Wrt_ID()
		{
			m_idx_byte += 2;
			m_rem_ui16--;
		}

		public void Wrt_ID_param_At(int idx_byte, byte id, byte param)
		{
			m_buf[idx_byte] = id;
			m_buf[idx_byte + 1] = param;
		}
	}
}
