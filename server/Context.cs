//#define CREATE_TEST_LEXED_CODE

using System;
using System.Text;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;

namespace md_svr
{

///////////////////////////////////////////////////////////////////////////////////////
// WS_Buf_Pool

static class WS_Buf_Pool
{
	const int EN_mem_add_size = 25 * 1024;  // 追加サイズは 25 kbytes
	static int ms_cur_mem_size = 25 * 1024;  // 初期サイズは 25 kbytes

	public class MemBlock : IDisposable
	{
		public MemBlock m_next_blk = null;
		public byte[] m_ary_buf = null;
		public bool mb_IsUsed = false;

		public void Dispose()
		{
			mb_IsUsed = false;
		}
	}
	static MemBlock ms_mem_blk_1st = null;
	static MemBlock ms_mem_blk_last = null;

	// -------------------------------------------------------------------------
	public static MemBlock Lease_MemBlock()
	{
		for (MemBlock mem_blk = ms_mem_blk_1st; mem_blk != null; mem_blk = mem_blk.m_next_blk)
		{
			if (mem_blk.mb_IsUsed == false)
			{
				mem_blk.mb_IsUsed = true;
				return mem_blk;
			}
		}

		MemBlock new_mem_blk = Create_NewBlk();
		new_mem_blk.mb_IsUsed = true;
		return new_mem_blk;
	}

	// -------------------------------------------------------------------------
	static MemBlock Create_NewBlk()
	{
		MemBlock ret_mem_blk = new MemBlock();
		ret_mem_blk.m_ary_buf = new byte[ms_cur_mem_size];

		if (ms_mem_blk_1st == null)
		{
			ms_mem_blk_1st = ret_mem_blk;
			ms_mem_blk_last = ret_mem_blk;
		}
		else
		{
			ms_mem_blk_last.m_next_blk = ret_mem_blk;
			ms_mem_blk_last = ret_mem_blk;
		}
		return ret_mem_blk;
	}

} // WS_Buf_Pool

///////////////////////////////////////////////////////////////////////////////////////
// WS_Context

class WS_Context
{
// -------------------------------------------------
// MdSvr.Spawn_Start() にて設定される
public static CancellationTokenSource ms_cts_shutdown;
public static UnicodeEncoding ms_utf16_encoding;

// -------------------------------------------------
//const int EN_bytes_buf = 100 * 1024;
//byte[] m_ws_buf = new byte[EN_bytes_buf];  // 100 kbytes（送信用にこのサイズを考えているけど、ここまでは不必要？？）

Read_WS_Buf m_read_WS_buf = null;
Write_WS_Buffer m_write_WS_buf = null;

WebSocket m_WS = null;

// ------------------------------------------------------------------------------------
public async Task Spawn_Context(HttpListenerContext context)
{
	// AcceptWebSocketAsync() は、キャンセルトークンをサポートしていない
	// 引数： サポートされている WebSocket サブプロトコル
	HttpListenerWebSocketContext wsc = await context.AcceptWebSocketAsync(null);
	MainForm.StdOut("--- WebSocket 接続完了\r\n");

	using (m_WS = wsc.WebSocket)
	using (WS_Buf_Pool.MemBlock mem_blk = WS_Buf_Pool.Lease_MemBlock())
	{
		m_read_WS_buf = new Read_WS_Buf(mem_blk.m_ary_buf);
		m_write_WS_buf = new Write_WS_Buffer(mem_blk.m_ary_buf);

		try
		{
			// ＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜
#if CREATE_TEST_LEXED_CODE
			// Test Lexed Code を送信する
			Write_WS_Buffer write_ws_buf = MainForm.ms_DBG_write_WS_buf;
			await m_WS.SendAsync(
				new ArraySegment<byte>(write_ws_buf.Get_buf(), 0, write_ws_buf.Get_idx_byte_cur())
				, WebSocketMessageType.Binary, true, ms_cts_shutdown.Token);
#else
			// まずはルートフォルダのファイル情報を送信しておく
			FileLister.Set_DirFileNames(m_write_WS_buf, "./");

			await m_WS.SendAsync(
					new ArraySegment<byte>(mem_blk.m_ary_buf, 0, m_write_WS_buf.Get_idx_byte_cur())
					, WebSocketMessageType.Binary, true, ms_cts_shutdown.Token);
#endif
			while (true)
			{
				WebSocketReceiveResult rslt = await m_WS.ReceiveAsync(
						new ArraySegment<byte>(mem_blk.m_ary_buf), ms_cts_shutdown.Token);
				
				if (rslt.EndOfMessage == false)
				{
					MainForm.StdOut("--- 不正な大きさのデータを受信しました。クライアントを切断します\r\n");
					break;
				}
				if (m_WS.State == WebSocketState.CloseReceived)
				{
					MainForm.StdOut("--- クライアントが接続を Close しました\r\n");
					break;
				}

				// 今は、index.md のみを解析するようにしている
//						string ret_str = Lexer.LexFile(m_write_WS_buf, "md_root/index.md");

				string str_recv = ms_utf16_encoding.GetString(mem_blk.m_ary_buf, 0, rslt.Count);
				MainForm.StdOut(str_recv);
			}

			await m_WS.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "接続を終了します", ms_cts_shutdown.Token);
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


} // WS_Context
} // md_svr
