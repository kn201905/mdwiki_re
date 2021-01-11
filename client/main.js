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
			alert('!!! m_state != STT_Null の状態で、接続を開始しようとしました。');
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
			alert('g_WS にて onerror を受信しました。console を確認してください。');
			console.log('!!! onerror -> ' + event);
		};

		m_ws.onmessage = (event) => {
			g_TStream_Reader.SetArrayBuffer(event.data);
			
			let id;
			switch (id = g_TStream_Reader.Consume_NextID()) {
			case ID_MD_file:
				ID_MD_file_Handler();
				return;

			case ID_DirFileList:
				g_pnl_Log.Log_no_LF('--- ID_DirFileList を受信しました。▶ ');
				g_FileLister.Consume_DirFileList();
				return;
				
			case ID_Files_inDir:
				g_pnl_Log.Log_no_LF('--- ID_Files_inDir を受信しました。▶ ');
				g_FileLister.Consume_Files_inDir();
				return;

			default:
				const err_msg = 'g_WS.onmessage() : 不明な ID を受信しました。/ id = ' + id;
				alert(err_msg);
				throw new Error(err_msg);
			}
		};
	};

	this.Send_Write_Buf = () => {
		if (m_state != STT_Conncted) {
			alert('!!! m_state != STT_Conncted の状態で、メッセージを送信しようとしました。');
			return;
		}
//		g_pnl_Log.Log('--- g_Write_Buf の送信実行');
		m_ws.send(g_Write_Buf.Get_u16ary_Cur());
	};

	this.Close = () => {
		if (m_state != STT_Conncted) {
			alert('!!! m_state != STT_Conncted の状態で、接続を切断をしようとしました。');
			return;
		}

		m_state = STT_Closing;
		g_pnl_Log.Log('--- 切断開始...');
		g_GUI.Update(STT_Closing);

		// 1000 -> CloseEvent : Normal Closure
		m_ws.close(1000, "Closing from client");
	};
};

// -----------------------------------------------------------------------
const g_collection_e_dom_tree = new function() {
	// key -> str_path_file_without_ext（.md は付けないこと）
	// value -> [SEC_Created, e_dom_tree]
	const m_e_dom_trees = {};
	
	const ID_SEC_Created = 0;
	const ID_e_dom_tree = 1;
	
	this.Get_e_dom_tree = (str_path_file_without_ext) => {
		const val = m_e_dom_trees[str_path_file_without_ext];
		if (val) { return val[ID_e_dom_tree]; }
		
		const err_msg = 'g_collection_e_dom_tree.Get_e_dom_tree() : 指定されたものが見つかりませんでした\n'
				+ '指定されたファイル -> ' + str_path_file_without_ext;
		alert(err_msg);
		throw new Error(err_msg);
	};
	
	// g_TStream_Reader を用いて、dom_tree を生成する
	// g_TStream_Reader は、ID_Num_int を読み出した直後の状態であること
	this.Create_e_dom_tree = (str_path_file_without_ext, SEC_Created) => {
		const e_new_oframe_div_dom_tree = g_DomTree.BuildTree();
		m_e_dom_trees[str_path_file_without_ext] = [SEC_Created, e_new_oframe_div_dom_tree];
		return e_new_oframe_div_dom_tree;
	};
	
	this.Delete_e_dom_tree_if_exists = (str_path_file_without_ext) => {
		const val = m_e_dom_trees[str_path_file_without_ext];
		if (val) {
			delete m_e_dom_trees[str_path_file_without_ext];
			g_pnl_Log.Log('e_dom_tree を削除しました ▶ ' + str_path_file_without_ext);
			return;
		}
		g_pnl_Log.Log('削除対象の e_dom_tree は存在しませんでした ▶ ' + str_path_file_without_ext);
	};
	
	// 対象となる e_dom_tree が存在しない場合、0 が返される
	this.Get_SEC_Created_if_exists = (str_path_file_without_ext) => {
		const val = m_e_dom_trees[str_path_file_without_ext];
		if (val) {
			return val[ID_SEC_Created];
		}
		else {
			return 0;
		}
	};
	
	this.Delete_on_dir = (str_dir_path, ary_e_div_file) => {
		for (e_div of ary_e_div_file) {
			let str_path = str_dir_path + e_div.textContent;
			let val = m_e_dom_trees[str_path];
			if (val) {
				delete m_e_dom_trees[str_path];
				g_pnl_Log.Log('e_dom_tree を削除しました ▶ ' + str_path);
			}
		}
	};
};


const ID_MD_file_Handler = () => {
	g_pnl_Log.Log_no_LF('--- ID_MD_file を受信しました。▶ ');
	let id;
	
	// パス名の取得
	if ((id = g_TStream_Reader.Consume_NextID()) != ID_Text)
	{ throw new Error("g_WS.m_ws.onmessage() : expect -> path_dir / id = " + id); }
	const path_dir = g_TStream_Reader.Get_text_cur();
	
	// ファイル名の取得
	if ((id = g_TStream_Reader.Consume_NextID()) != ID_Text)
	{ throw new Error("g_WS.m_ws.onmessage() : expect -> file_name / id = " + id); }
	
	// file_name には「.md」が付加されていないことに注意
	const file_name = g_TStream_Reader.Get_text_cur();
	const str_path_file_without_ext = path_dir + file_name;

	g_pnl_Log.Log(str_path_file_without_ext);
	
	// SEC_Created の取得
	switch (id = g_TStream_Reader.Consume_NextID())
	{
	case ID_End: {  // クライアント側の dom_tree の更新が不要の場合
		g_pnl_Log.Log('　　　保持している dom_tree を利用します。');
		const e_child_cur = g_e_DomTreeArea.firstChild;
		const e_reused_dom_tree = g_collection_e_dom_tree.Get_e_dom_tree(str_path_file_without_ext);
		if (e_child_cur == e_reused_dom_tree) { return; }
		
		g_e_DomTreeArea.removeChild(e_child_cur);
		g_e_DomTreeArea.appendChild(e_reused_dom_tree); 
	} return;

	case ID_Num_int: {  // dom_tree の生成を実行が指定された場合（生成し直しも含む）
		g_pnl_Log.Log('　　　新規に dom_tree を生成します。');
		const SEC_Created = g_TStream_Reader.Get_num_int();
		
		const e_new_dom_tree =
				g_collection_e_dom_tree.Create_e_dom_tree(str_path_file_without_ext, SEC_Created);
		
		const e_child_cur = g_e_DomTreeArea.firstChild;
		if (e_child_cur) { g_e_DomTreeArea.removeChild(e_child_cur); }

		g_e_DomTreeArea.appendChild(e_new_dom_tree);
	} return;
	
	case ID_MD_file_Deleted: {
		g_pnl_Log.Log('　　　サーバー側でファイルが削除されていました。');
		alert('サーバー側で、ファイルが削除されています。\n'
				+ 'ディレクトリをリロードしてください。\nファイル一覧の再表示、及び、リソースの解放が実行されます。');
		} return;
		
	default: {
		const err_msg = 'ID_MD_file_Handler() : 不明な ID を受信しました。/ id = ' + id;
		alert(err_msg);
		throw new Error(err_msg);
		}
	}
};


// -----------------------------------------------------------------------
const g_GUI = new function() {
	this.Update = (stt) => {
		g_pnl_SvrConnect.GUI_Update(stt);
	};
};

const g_pnl_SvrConnect = new function() {
	let mb_show_URL_pnl = true;
	
	const m_e_panel = g_e_body.Add_Div();
	m_e_panel.style.marginLeft = '1.5rem';
	m_e_panel.style.display = 'flex';
	
	const m_e_pnl_URL = m_e_panel.Add_Div();
	m_e_pnl_URL.Add_TxtNode('Server URL: ');

	const m_e_input_URL = m_e_pnl_URL.Add_Input();
	m_e_input_URL.value = g_str_URL_init;
	m_e_input_URL.style.marginRight = '1em';

	const m_e_btn_connect = m_e_pnl_URL.Add_Btn('Connect');
	m_e_btn_connect.style.marginRight = '1em';
	m_e_btn_connect.onclick = () => {
		m_e_btn_show_hide.onclick();
		g_WS.ConnectTo(m_e_input_URL.value);
	}

	const m_e_btn_close = m_e_pnl_URL.Add_Btn('Close');
	m_e_btn_close.style.marginRight = '1em';
	m_e_btn_close.onclick = () => { g_WS.Close(); }

	const m_e_btn_show_hide = m_e_panel.Add_Btn('URL Hide');
	m_e_btn_show_hide.onclick = () => {
		if (mb_show_URL_pnl == true) {
			m_e_pnl_URL.style.display = 'none';
			m_e_btn_show_hide.textContent = 'URL Show'
			mb_show_URL_pnl = false;
			
			m_e_panel.style.float = 'right';
		}
		else {
			m_e_pnl_URL.style.display = 'block';
			m_e_btn_show_hide.textContent = 'URL Hide'
			mb_show_URL_pnl = true;

			m_e_panel.style.float = 'none';
		}
	}

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
	const m_e_btn_show_log = g_e_body.Add_Div();
	m_e_btn_show_log.classList.add('btn_float', 'Log_show_btn');
	m_e_btn_show_log.textContent = 'ログ表示';
//	m_e_btn_show_log.style.display = 'none';
	m_e_btn_show_log.onclick = () => {
		m_e_panel.style.display = 'block';
		m_e_btn_show_log.style.display = 'none';
	}
	
	const m_e_panel = g_e_body.Add_Div();
	m_e_panel.classList.add('Log_pnl');
	m_e_panel.style.display = 'none';  // 初期状態では、ログは非表示にしておく

	const e_stg = m_e_panel.Add_FlexStg();
	e_stg.style.margin = '5px';

	const e_title = e_stg.Add_Div();
	e_title.style.fontWeight = 'bold';
	e_title.textContent = '--- Log ---　';

	const m_e_btn_clear_log = e_stg.Add_DivBtn('ログ クリア');
	m_e_btn_clear_log.style.fontSize = '0.7rem'
	m_e_btn_clear_log.style.marginRight = '1rem';
	m_e_btn_clear_log.onclick = () => { e_txt_area.value = ''; };

	const m_e_btn_hide_log = e_stg.Add_DivBtn('ログ非表示');
	m_e_btn_hide_log.style.fontSize = '0.7rem'
	m_e_btn_hide_log.onclick = () => {
		m_e_panel.style.display = 'none';
		m_e_btn_show_log.style.display = 'block';
	}

	// --------------------------------------------------------------
	const e_txt_area = m_e_panel.Add_TxtArea();
	e_txt_area.classList.add('Log_text_area');

	this.Log = (msg) => {
		e_txt_area.value += msg + "\n";
		e_txt_area.scrollTop = e_txt_area.scrollHeight;
	};
	
	this.Log_no_LF = (msg) => {
		e_txt_area.value += msg;
		e_txt_area.scrollTop = e_txt_area.scrollHeight;
	};
};

g_FileLister.Set_e_panel(g_e_body.Add_Div());

const g_e_DomTreeArea = g_e_body.Add_Div();

g_GUI.Update(STT_Null);

