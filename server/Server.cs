//using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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

		static List<Task> m_tasks_context = new List<Task>();
		static bool msb_in_shutting_down = false;

		// ------------------------------------------------------------------------------------
		public static async Task Spawn_Start()
		{
			var listener = new HttpListener();

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

						foreach(Task task in m_tasks_context)
						{ await task; }

						break;
					}

					var ws_context = new WS_Context();
					Task task_context = ws_context.Spawn_Context(context);
					m_tasks_context.Add(task_context);
					Spawn_ContextMonitor(task_context);
				}
			}

			listener.Stop();
			await Task.Delay(1000);  // これが無いと例外が発生することがあるような？
			listener.Close();
		}

		// ------------------------------------------------------------------------------------
		static async void Spawn_ContextMonitor(Task task_context)
		{
			await task_context;

			// シャットダウン中の foreach(Task task in m_tasks_context) において、m_tasks_context の変更はできないため
			if (msb_in_shutting_down == true) { return; }

			m_tasks_context.Remove(task_context);
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

