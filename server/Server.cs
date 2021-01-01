//using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace md_svr
{
	static class MdSvr
	{
		// WebSocket 用のポート番号
		enum WS_Port : uint { EN_num = 3000 }
		static string ms_str_port_num = ((uint)WS_Port.EN_num).ToString();

		// -------------------------------------------------
		public static CancellationTokenSource ms_cts_shutdown;
		public static UnicodeEncoding ms_utf16_encoding = null;

		static SortedList<uint, Task> m_task_list = new SortedList<uint, Task>();
		static bool msb_in_shutting_down = false;

		// ------------------------------------------------------------------------------------
		public static async Task Spawn_Start()
		{
			var listener = new HttpListener();
			uint idx_context = 0;

			listener.Prefixes.Add($"http://localhost:{ms_str_port_num}/");
			MainForm.StdOut($"--- 接続受付開始（ポート: {ms_str_port_num}）\r\n");

			listener.Start();
			using (ms_cts_shutdown = new CancellationTokenSource())
			{
				// WS_Context の static 変数を設定
				WS_Context.ms_cts_shutdown = ms_cts_shutdown;
				WS_Context.ms_utf16_encoding = ms_utf16_encoding;

				// Read_Buffer の static 変数を設定
				Read_WS_Buf.ms_utf16_encoding = ms_utf16_encoding;

				while (true)
				{
					// GetContextAsync() は、キャンセルトークンをサポートしていない
					HttpListenerContext context = await listener.GetContextAsync();
					if (context.Request.IsWebSocketRequest == false)
					{
						// この場合、サーバーを終了するメッセージを受け取ったものとする
						// GetContextAsync() にキャンセルトークンがないための措置
						MainForm.StdOut("--- シャットダウン処理開始\r\n");

						msb_in_shutting_down = true;
						ms_cts_shutdown.Cancel();

						foreach(var kvp in m_task_list)
						{
//							Debug.WriteLine("--- await idx_context : " + kvp.Key.ToString());
							await kvp.Value;
						}

						break;
					}

					var ws_context = new WS_Context();
					Task task_context = ws_context.Spawn_Context(context, ++idx_context);
					m_task_list.Add(idx_context, task_context);
				}
			}

			listener.Stop();
			await Task.Delay(1000);  // これが無いと例外が発生することがあるような？
			listener.Close();
		}

		// ------------------------------------------------------------------------------------
		public static void Remove_task_context(uint idx_context)
		{
			// シャットダウン中の foreach(var kvp in m_task_list) において、m_task_list の変更はできないため
			if (msb_in_shutting_down == true) { return; }

			m_task_list.Remove(idx_context);
		}

		// ------------------------------------------------------------------------------------
		public static void SendSignal_Shutdown()
		{
			// 現時点では、"CLOSE" の文字列は任意のものでよい
			var http_content = new StringContent("CLOSE", ms_utf16_encoding);
			new HttpClient().PostAsync($"http://localhost:{ms_str_port_num}/", http_content);
		}
	}
}

