
namespace md_svr
{
	class MdSvr
	{
		// WebSocket 用のポート番号
		enum WS_Port : uint { EN_num = 3000 }
		static string ms_str_port_num = ((uint)WS_Port.EN_num).ToString();

		// -------------------------------------------------
		HttpListener m_listener = new HttpListener();
		List<Task> m_tasks_listener = new List<Task>();
		bool m_bSignal_shutdown = false;
		CancellationTokenSource m_cts_shutdown;

		// false: little endian / true: BOM付加 / true: 例外スローあり
		UnicodeEncoding m_utf16_encoding = new UnicodeEncoding(false, true, true);

		// ------------------------------------------------------------------------------------
		public async Task Spawn_Start()
		{
			m_listener.Prefixes.Add($"http://localhost:{ms_str_port_num}/");
			MainForm.StdOut($"--- 接続受付開始（ポート: {ms_str_port_num}）\r\n");

			m_listener.Start();
			using (m_cts_shutdown = new CancellationTokenSource())
			{
				while (true)
				{
					// GetContextAsync() は、キャンセルトークンをサポートしていない
					HttpListenerContext context = await m_listener.GetContextAsync();
					m_tasks_listener.Add(Spawn_Context(context));
					if (m_bSignal_shutdown == true)
					{
						m_cts_shutdown.Cancel();

						foreach(Task task in m_tasks_listener)
						{ await task; }

						break;
					}
				}
			}

			m_listener.Stop();
			await Task.Delay(1000);  // これが無いと例外が発生することがあるような？
			m_listener.Close();
		}

		// ------------------------------------------------------------------------------------
		async Task Spawn_Context(HttpListenerContext context)
		{
			if (context.Request.IsWebSocketRequest == false)
			{
				m_bSignal_shutdown = true;  // シャットダウンシグナル context の終了

				// この場合、サーバーを終了するメッセージを受け取ったものとする
				// GetContextAsync() にキャンセルトークンがないための措置
				// TODO: キャンセルトークンの処理
				MainForm.StdOut("--- シャットダウン処理開始\r\n");
				return;
			}

			// AcceptWebSocketAsync() は、キャンセルトークンをサポートしていない
			// 引数： サポートされている WebSocket サブプロトコル
			HttpListenerWebSocketContext wsc = await context.AcceptWebSocketAsync(null);
			MainForm.StdOut("--- WebSocket 接続完了\r\n");

			using (WebSocket ws = wsc.WebSocket)
			{
				try
				{
					var ws_buf = new ArraySegment<byte>(new byte[100]);
					await ws.ReceiveAsync(ws_buf, m_cts_shutdown.Token);

					await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "接続を終了します", m_cts_shutdown.Token);
				}
				catch (OperationCanceledException)
				{
					MainForm.StdOut("--- サーバーシャットダウンのシグナルを受信しました\r\n");
				}
			}

			MainForm.StdOut("--- WebSocket 切断完了\r\n");
		}

		// ------------------------------------------------------------------------------------
		public void SendSignal_Shutdown()
		{
			// 現時点では、"CLOSE" の文字列は任意のものでよい
			var http_content = new StringContent("CLOSE", m_utf16_encoding);
			new HttpClient().PostAsync($"http://localhost:{ms_str_port_num}/", http_content);
		}
	}
}
