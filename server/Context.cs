#define CREATE_TEST_LEXED_CODE

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
		// MdSvr.Spawn_Start() にて設定される
		public static CancellationTokenSource ms_cts_shutdown;
		public static UnicodeEncoding ms_utf16_encoding;

		// -------------------------------------------------
		const int EN_bytes_buf = 100 * 1024;
		byte[] m_ws_buf = new byte[EN_bytes_buf];  // 100 kbytes（送信用にこのサイズを考えているけど、ここまでは不必要？？）

		Read_WS_Buf m_read_WS_buf;
		Write_WS_Buffer m_write_WS_buf;

		string m_str_root_dir = "md_root/";
		string m_str_cur_dir = "md_root/";
		WebSocket m_ws;

		public WS_Context()
		{
			m_read_WS_buf = new Read_WS_Buf(m_ws_buf);
			m_write_WS_buf = new Write_WS_Buffer(m_ws_buf);
		}

		// ------------------------------------------------------------------------------------
		public async Task Spawn_Context(HttpListenerContext context)
		{
			// AcceptWebSocketAsync() は、キャンセルトークンをサポートしていない
			// 引数： サポートされている WebSocket サブプロトコル
			HttpListenerWebSocketContext wsc = await context.AcceptWebSocketAsync(null);
			MainForm.StdOut("--- WebSocket 接続完了\r\n");

			var seg_for_recv = new ArraySegment<byte>(m_ws_buf);
			using (m_ws = wsc.WebSocket)
			{
				try
				{
					// ＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜
#if CREATE_TEST_LEXED_CODE
					// Test Lexed Code を送信する
					Write_WS_Buffer write_ws_buf = MainForm.ms_DBG_write_WS_buf;
					await m_ws.SendAsync(
						new ArraySegment<byte>(write_ws_buf.Get_buf(), 0, write_ws_buf.Get_idx_byte_cur())
						, WebSocketMessageType.Binary, true, ms_cts_shutdown.Token);
#else
					// まずはルートフォルダのファイル情報を送信しておく
					if (await SendFileNames_CurDir() == false)
					{ throw new Exception("接続開始直後の初期化通信に失敗しました"); }
#endif
					while (true)
					{
						WebSocketReceiveResult rslt = await m_ws.ReceiveAsync(seg_for_recv, ms_cts_shutdown.Token);
						if (rslt.EndOfMessage == false)
						{
							MainForm.StdOut("--- 不正な大きさのデータを受信しました。クライアントを切断します\r\n");
							break;
						}
						if (m_ws.State == WebSocketState.CloseReceived)
						{
							MainForm.StdOut("--- クライアントが接続を Close しました\r\n");
							break;
						}

						// 今は、index.md のみを解析するようにしている
//						string ret_str = Lexer.LexFile(m_write_WS_buf, "md_root/index.md");

						string str_recv = ms_utf16_encoding.GetString(m_ws_buf, 0, rslt.Count);
						MainForm.StdOut(str_recv);
					}

					await m_ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "接続を終了します", ms_cts_shutdown.Token);
				}
				catch (OperationCanceledException)
				{
					MainForm.StdOut("--- サーバーシャットダウンのシグナルを受信しました\r\n");
				}
				catch (WebSocketException ex)
				{
					MainForm.StdOut($"!!! 例外発を補足しました WebSoketErrorCode : {ex.WebSocketErrorCode.ToString()}\r\n");
					MainForm.StdOut($"    {ex.ToString()}\r\n");
				}
				catch (Exception ex)
				{
					MainForm.StdOut($"!!! 例外を補足しました : {ex.ToString()}\r\n");
				}
			}

			MainForm.StdOut("--- WebSocket 切断完了\r\n");
		}

		// ------------------------------------------------------------------------------------
		// 送信に失敗した場合、false が返される
		async Task<bool> SendFileNames_CurDir()
		{
			m_write_WS_buf.Flush();

			try
			{
				// -------------------------------------------------
				// ディレクトリ名の処理
				{
					int idx_byte_AtDNames = m_write_WS_buf.Get_idx_byte_cur();
					m_write_WS_buf.Skip_Wrt_ID();

					var dirs = Directory.EnumerateDirectories(m_str_cur_dir);
					int cnt = 0;
					foreach(string dname in dirs)
					{
						m_write_WS_buf.Wrt_PFName(dname);
						cnt++;
					}
					if (cnt > 255) { throw new Exception("ディレクトリの個数が 255 個を超えています。"); }

					m_write_WS_buf.Wrt_ID_param_At(idx_byte_AtDNames, ID.Directory_Names, (byte)cnt);
				}

				// -------------------------------------------------
				// ファイル名の処理
				{
					int idx_byte_AtFName = m_write_WS_buf.Get_idx_byte_cur();
					m_write_WS_buf.Skip_Wrt_ID();

					var files = Directory.EnumerateFiles(m_str_cur_dir);
					int cnt = 0;
					foreach(string fname in files)
					{
						m_write_WS_buf.Wrt_PFName(fname);
						cnt++;
					}
					if (cnt > 255) { throw new Exception("ファイルの個数が 255 個を超えています。"); }
					m_write_WS_buf.Wrt_ID_param_At(idx_byte_AtFName, ID.File_Names, (byte)cnt);
				}

				// -------------------------------------------------
				// ファイル送信
				m_write_WS_buf.Wrt_ID_End();

				await m_ws.SendAsync(new ArraySegment<byte>(m_ws_buf, 0, m_write_WS_buf.Get_idx_byte_cur())
					, WebSocketMessageType.Binary, true, ms_cts_shutdown.Token);
			}
			catch (Exception ex)
			{
				MainForm.StdOut($"!!! 例外を補足しました : {ex.ToString()}\r\n");
				return false;
			}

			return true;
		}

		// ------------------------------------------------------------------------------------
		// デバッグ用
		// idx: pcs の位置
		(int, string) ReadPStr_frm_WSBuf(int idx_buf)
		{
			int pcs = m_ws_buf[idx_buf] + m_ws_buf[idx_buf + 1] * 256;
			return (idx_buf + 2 + pcs * 2, ms_utf16_encoding.GetString(m_ws_buf, idx_buf + 2, pcs * 2));
		}
	}
} 
