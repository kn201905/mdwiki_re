using System;
using System.IO;
using System.Text;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;

namespace md_svr
{
	class WS_Context
	{
		// -------------------------------------------------
		// MdSvr.Spawn_Start() �ɂĐݒ肳���
		public static CancellationTokenSource ms_cts_shutdown;
		public static UnicodeEncoding ms_utf16_encoding;

		// -------------------------------------------------
		const int EN_bytes_buf = 100 * 1024;
		byte[] m_ws_buf = new byte[EN_bytes_buf];  // 100 kbytes�i���M�p�ɂ��̃T�C�Y���l���Ă��邯�ǁA�����܂ł͕s�K�v�H�H�j

		Read_Buffer m_read_buf;
		Write_Buffer m_write_buf;

		string m_str_cur_dir = "md_root/";
		WebSocket m_ws;

		public WS_Context()
		{
			m_read_buf = new Read_Buffer(m_ws_buf);
			m_write_buf = new Write_Buffer(m_ws_buf);
		}

		// ------------------------------------------------------------------------------------
		public async Task Spawn_Context(HttpListenerContext context)
		{
			// AcceptWebSocketAsync() �́A�L�����Z���g�[�N�����T�|�[�g���Ă��Ȃ�
			// �����F �T�|�[�g����Ă��� WebSocket �T�u�v���g�R��
			HttpListenerWebSocketContext wsc = await context.AcceptWebSocketAsync(null);
			MainForm.StdOut("--- WebSocket �ڑ�����\r\n");

			var seg_for_recv = new ArraySegment<byte>(m_ws_buf);
			using (m_ws = wsc.WebSocket)
			{
				try
				{
					// �܂��̓��[�g�t�H���_�̃t�@�C�����𑗐M���Ă���
					if (await SendFileNames_CurDir() == false)
					{ throw new Exception("�ڑ��J�n����̏������ʐM�Ɏ��s���܂���"); }

					while (true)
					{
						WebSocketReceiveResult rslt = await m_ws.ReceiveAsync(seg_for_recv, ms_cts_shutdown.Token);
						if (rslt.EndOfMessage == false)
						{
							MainForm.StdOut("--- �s���ȑ傫���̃f�[�^����M���܂����B�N���C�A���g��ؒf���܂�\r\n");
							break;
						}
						if (m_ws.State == WebSocketState.CloseReceived)
						{
							MainForm.StdOut("--- �N���C�A���g���ڑ��� Close ���܂���\r\n");
							break;
						}

						string str_recv = ms_utf16_encoding.GetString(m_ws_buf, 0, rslt.Count);
						MainForm.StdOut(str_recv);
					}

					await m_ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "�ڑ����I�����܂�", ms_cts_shutdown.Token);
				}
				catch (OperationCanceledException)
				{
					MainForm.StdOut("--- �T�[�o�[�V���b�g�_�E���̃V�O�i������M���܂���\r\n");
				}
				catch (WebSocketException ex)
				{
					MainForm.StdOut($"!!! ��O����⑫���܂��� WebSoketErrorCode : {ex.WebSocketErrorCode.ToString()}\r\n");
					MainForm.StdOut($"    {ex.ToString()}\r\n");
				}
				catch (Exception ex)
				{
					MainForm.StdOut($"!!! ��O��⑫���܂��� : {ex.ToString()}\r\n");
				}
			}

			MainForm.StdOut("--- WebSocket �ؒf����\r\n");
		}

		// ------------------------------------------------------------------------------------
		// ���M�Ɏ��s�����ꍇ�Afalse ���Ԃ����
		async Task<bool> SendFileNames_CurDir()
		{
			m_write_buf.Flush();

			try
			{
				// -------------------------------------------------
				// �f�B���N�g�����̏���
				{
					int idx_byte_AtDNames = m_write_buf.Get_idx_byte_cur();
					m_write_buf.Skip_Wrt_ID();

					var dirs = Directory.EnumerateDirectories(m_str_cur_dir);
					int cnt = 0;
					foreach(string dname in dirs)
					{
						m_write_buf.Wrt_PFName(dname);
						cnt++;
					}
					if (cnt > 255) { throw new Exception("�f�B���N�g���̌��� 255 �𒴂��Ă��܂��B"); }

					m_write_buf.Wrt_ID_param_At(idx_byte_AtDNames, (byte)ID.Directory_Names, (byte)cnt);
				}

				// -------------------------------------------------
				// �t�@�C�����̏���
				{
					int idx_byte_AtFName = m_write_buf.Get_idx_byte_cur();
					m_write_buf.Skip_Wrt_ID();

					var files = Directory.EnumerateFiles(m_str_cur_dir);
					int cnt = 0;
					foreach(string fname in files)
					{
						m_write_buf.Wrt_PFName(fname);
						cnt++;
					}
					if (cnt > 255) { throw new Exception("�t�@�C���̌��� 255 �𒴂��Ă��܂��B"); }
					m_write_buf.Wrt_ID_param_At(idx_byte_AtFName, (byte)ID.File_Names, (byte)cnt);
				}

				// -------------------------------------------------
				// �t�@�C�����M
				m_write_buf.Wrt_ID((byte)ID.End);

				await m_ws.SendAsync(new ArraySegment<byte>(m_ws_buf, 0, m_write_buf.Get_idx_byte_cur())
					, WebSocketMessageType.Binary, true, ms_cts_shutdown.Token);
			}
			catch (Exception ex)
			{
				MainForm.StdOut($"!!! ��O��⑫���܂��� : {ex.ToString()}\r\n");
				return false;
			}

			return true;
		}

		// ------------------------------------------------------------------------------------
		// �f�o�b�O�p
		// idx: pcs �̈ʒu
		(int, string) ReadPStr_frm_WSBuf(int idx_buf)
		{
			int pcs = m_ws_buf[idx_buf] + m_ws_buf[idx_buf + 1] * 256;
			return (idx_buf + 2 + pcs * 2, ms_utf16_encoding.GetString(m_ws_buf, idx_buf + 2, pcs * 2));
		}
	}
}
