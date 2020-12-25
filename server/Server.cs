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

		// false: little endian / true: BOM付加 / true: 例外スローあり
		public static UnicodeEncoding ms_utf16_encoding = new UnicodeEncoding(false, true, true);

		// ------------------------------------------------------------------------------------
		public static async Task Spawn_Start()
		{
			var listener = new HttpListener();
			var tasks_context = new List<Task>();

			listener.Prefixes.Add($"http://localhost:{ms_str_port_num}/");
			MainForm.StdOut($"--- 接続受付開始（ポート: {ms_str_port_num}）\r\n");

			listener.Start();
			using (ms_cts_shutdown = new CancellationTokenSource())
			{
				// WS_Context の static 変数を設定
				WS_Context.ms_cts_shutdown = ms_cts_shutdown;
				WS_Context.ms_utf16_encoding = ms_utf16_encoding;

				// Read_Buffer の static 変数を設定
				Read_Buffer.ms_utf16_encoding = ms_utf16_encoding;

				while (true)
				{
					// GetContextAsync() は、キャンセルトークンをサポートしていない
					HttpListenerContext context = await listener.GetContextAsync();
					if (context.Request.IsWebSocketRequest == false)
					{
						// この場合、サーバーを終了するメッセージを受け取ったものとする
						// GetContextAsync() にキャンセルトークンがないための措置
						MainForm.StdOut("--- シャットダウン処理開始\r\n");

						ms_cts_shutdown.Cancel();

						foreach(Task task in tasks_context)
						{ await task; }

						break;
					}

					var ws_context = new WS_Context();
					tasks_context.Add(ws_context.Spawn_Context(context));
				}
			}

			listener.Stop();
			await Task.Delay(1000);  // これが無いと例外が発生することがあるような？
			listener.Close();
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

