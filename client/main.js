'use strict';
const g_str_URL_init = "ws://localhost:3000";

const STT_Null = 1;
const STT_Connecting = 2;
const STT_Conncted = 3;
const STT_Closing = 4;

// -----------------------------------------------------------------------
const g_WS = new function() {
	let m_state = STT_Null;
	let m_ws = null;

	this.ConnectTo = (str_URL) => {
		if (m_state != STT_Null) {
			g_pnl_Log.Log('!!! m_state != STT_Null の状態で、接続を開始しようとしました。');
			return;
		}
		m_state = STT_Connecting;
		g_pnl_Log.Log('--- 接続開始...');
		g_GUI.Update(STT_Connecting);

		m_ws = new WebSocket(str_URL);
		m_ws.binaryType = 'arraybuffer';

		m_ws.onopen = () => {
			m_state = STT_Conncted;
			g_pnl_Log.Log('--- 接続完了');
			g_GUI.Update(STT_Conncted);
		};

		m_ws.onclose = () => {
			m_state = STT_Null;
			g_pnl_Log.Log('--- 切断完了');
			g_GUI.Update(STT_Null);

			m_ws = null;
		};

		m_ws.onerror = (event) => {
		};

		m_ws.onmessage = (event) => {
			g_TStream_Reader.SetArrayBuffer(event.data);

			switch (g_TStream_Reader.Consume_NextID()) {
			case ID_Lexed_MD:
				g_pnl_Log.Log('--- ID_Lexed_MD を受信しました。@main.js');
				// BuildTree に渡した要素は、子要素が全て remove されるため注意
				g_DomTree.BuildTree(g_e_DomTreeArea);
				return;

			case ID_DirFileList:
				g_pnl_Log.Log('--- ID_DirFileList を受信しました。@main.js');
				g_FileLister.Consume_DirFileList();
				return;
				
			case ID_Files_inDir:
				g_pnl_Log.Log('--- ID_Files_inDir を受信しました。@main.js');
				g_FileLister.Consume_Files_inDir();
				return;

			default:
				g_pnl_Log.Log("!!! 不明な ID を受信しました。@main.js");
			}
		};
	};

	this.Send_Write_Buf = () => {
		if (m_state != STT_Conncted) {
			g_pnl_Log.Log('!!! m_state != STT_Conncted の状態で、メッセージを送信しようとしました。');
			return;
		}
//		g_pnl_Log.Log('--- g_Write_Buf の送信実行');
		m_ws.send(g_Write_Buf.Get_u16ary_Cur());
	};

	this.Close = () => {
		if (m_state != STT_Conncted) {
			g_pnl_Log.Log('!!! m_state != STT_Conncted の状態で、接続を切断をしようとしました。');
			return;
		}

		m_state = STT_Closing;
		g_pnl_Log.Log('--- 切断開始...');
		g_GUI.Update(STT_Closing);

		m_ws.close(1000, "Closing from client");
	};
};

// -----------------------------------------------------------------------
const g_GUI = new function() {
	this.Update = (stt) => {
		g_pnl_SvrConnect.GUI_Update(stt);
	};
};

const g_pnl_SvrConnect = new function() {
	const m_e_panel = g_e_body.Add_Div();
	m_e_panel.style.margin = '5px 0';
	m_e_panel.Add_TxtNode('Server URL: ');

	const m_e_input_URL = m_e_panel.Add_Input();
	m_e_input_URL.value = g_str_URL_init;
	m_e_input_URL.style.marginRight = '1em';

	const m_e_btn_connect = m_e_panel.Add_Btn('Connect');
	m_e_btn_connect.style.marginRight = '1em';
	m_e_btn_connect.onclick = () => { g_WS.ConnectTo(m_e_input_URL.value); }

	const m_e_btn_close = m_e_panel.Add_Btn('Close');
	m_e_btn_close.onclick = () => { g_WS.Close(); }

	this.GUI_Update = (stt) => {
		let bIsEnable_input_URL = false;
		let bIsEnable_btn_connect = false;
		let bIsEnable_btn_close = false;

		switch (stt) {
		case STT_Null:
			bIsEnable_btn_connect = true;
			bIsEnable_input_URL = true;
			break;
		case STT_Conncted:
			bIsEnable_btn_close = true;
			break;
		}

		m_e_input_URL.disabled = !bIsEnable_input_URL;
		m_e_btn_connect.disabled = !bIsEnable_btn_connect;
		m_e_btn_close.disabled = !bIsEnable_btn_close;
	};
};

const g_pnl_Log = new function() {
	const m_e_panel = g_e_body.Add_Div();

	const e_stg = m_e_panel.Add_FlexStg();
	const e_title = e_stg.Add_Div();
	e_title.style.fontWeight = 'bold';
	e_title.style.margin = '5px';
	e_title.textContent = '--- Log ---　';

	const m_e_btn_clear_log = e_stg.Add_DivBtn('ログ クリア');
	m_e_btn_clear_log.style.fontSize = '0.7rem'
	m_e_btn_clear_log.onclick = () => { e_txt_area.value = ''; };

	const e_txt_area = m_e_panel.Add_TxtArea();
	e_txt_area.classList.add('Log_text_area');

	this.Log = (msg) => {
		e_txt_area.value += msg + "\n";
		e_txt_area.scrollTop = e_txt_area.scrollHeight;
	};
};

g_FileLister.Set_e_panel(g_e_body.Add_Div());

const g_e_DomTreeArea = g_e_body.Add_Div();

g_GUI.Update(STT_Null);
