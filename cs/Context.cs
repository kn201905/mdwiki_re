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
		string m_str_cur_dir = "md_root/";

		WebSocket m_ws;

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
					// まずはルートフォルダのファイル情報を送信しておく
					if (await SendFileNames_CurDir() == false)
					{ throw new Exception("接続開始直後の初期化通信に失敗しました"); }

					while (true)
					{
						WebSocketReceiveResult rslt = await m_ws.ReceiveAsync(seg_for_recv, ms_cts_shutdown.Token);
//						MainForm.StdOut($"--- 受信バイト数 : {rslt.Count.ToString()}\r\n");
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
			int idx_buf = 0;
			try
			{
				// -------------------------------------------------
				// ディレクトリ名の処理
				m_ws_buf[0] = (byte)ID.Directory_Name;
				idx_buf = 2;
				{
					var dirs = Directory.EnumerateDirectories(m_str_cur_dir);
					int cnt = 0;
					foreach(string dname in dirs)
					{
						idx_buf = this.WrtPFName_to_WSBuf(idx_buf, dname);
						cnt++;
					}
					if (cnt > 255) { throw new Exception("ディレクトリの個数が 255 個を超えています。"); }
					m_ws_buf[1] = (byte)cnt;
				}

				// -------------------------------------------------
				// ファイル名の処理
				m_ws_buf[idx_buf++] = (byte)ID.File_Name;
				int idx_buf_pcs_files = idx_buf++;
				{
					var files = Directory.EnumerateFiles(m_str_cur_dir);
					int cnt = 0;
					foreach(string fname in files)
					{
						idx_buf = this.WrtPFName_to_WSBuf(idx_buf, fname);
						cnt++;
					}
					if (cnt > 255) { throw new Exception("ファイルの個数が 255 個を超えています。"); }
					m_ws_buf[idx_buf_pcs_files] = (byte)cnt;
				}

				// -------------------------------------------------
				// ファイル送信
				await m_ws.SendAsync(new ArraySegment<byte>(m_ws_buf, 0, idx_buf)
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
		// 戻り値： 次の idx_buf
		// エラーのときは例外が投げられる
		int WrtPStr_to_WSBuf(int idx_buf, string src_str)
		{
			int len_str = src_str.Length;
			if (idx_buf + 2 + len_str * 2 > EN_bytes_buf)  // +2 は文字数書き込みの分
			{ throw new Exception("m_ws_buf が不足しています。"); }

			unsafe 
			{
				fixed (char* psrc_top = src_str)
				fixed (byte* pdst_top = m_ws_buf)
				{
					char* psrc = psrc_top;
					char* pdst = (char*)(pdst_top + idx_buf);

					*pdst++ = (char)len_str;
					for (; len_str > 0; --len_str)
					{ *pdst++ = *psrc++; }

					idx_buf = (int)(((byte*)pdst) - pdst_top);
				}
			}
			return idx_buf;
		}

		// ------------------------------------------------------------------------------------
		// 戻り値： 次の idx_buf
		// エラーのときは例外が投げられる
		// 最後の「/」以降のみが記録される
		int WrtPFName_to_WSBuf(int idx_buf, string src_str)
		{
			int len_str = src_str.Length;
			if (idx_buf + 2 + len_str * 2 > EN_bytes_buf)  // +2 は文字数書き込みの分
			{ throw new Exception("m_ws_buf が不足しています。"); }

			unsafe 
			{
				fixed (char* psrc_top = src_str)
				fixed (byte* pdst_top = m_ws_buf)
				{
					char* psrc_tmnt = psrc_top + len_str;
					char* pdst = (char*)(pdst_top + idx_buf);

					// まず、最後のセパレータを探す
					char* psrc = psrc_tmnt;
					while (*--psrc != '/') {}
					char len_to_wrt = (char)(psrc_tmnt - ++psrc);

					*pdst++ = len_to_wrt;
					for (; len_to_wrt > 0; --len_to_wrt)
					{ *pdst++ = *psrc++; }

					idx_buf = (int)(((byte*)pdst) - pdst_top);
				}
			}
			return idx_buf;
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
