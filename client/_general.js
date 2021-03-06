// 定形コード

const g_e_body = document.body;

// -----------------------------------------------------------------------
Element.prototype.Add_Element = function(tag) {
	const e_elm = document.createElement(tag);
	this.appendChild(e_elm);
	return e_elm;
};

Element.prototype.Add_Div = function() {
	const e_div = document.createElement('div');
	this.appendChild(e_div);
	return e_div;
};

Element.prototype.Add_DivTxt = function(txt) {
	const e_div = document.createElement('div');
	e_div.textContent = txt;
	this.appendChild(e_div);
	return e_div;
};

Element.prototype.Add_Btn = function(label) {
	const e_btn = document.createElement('button');
	e_btn.textContent = label;
	this.appendChild(e_btn);
	return e_btn;
};

Element.prototype.Add_DivBtn = function(label) {
	const e_div = document.createElement('div');
	const e_btn = document.createElement('button');
	e_btn.textContent = label;

	e_div.appendChild(e_btn);
	this.appendChild(e_div);
	return e_btn;
};

Element.prototype.Add_Input = function() {
	const e_input = document.createElement('input');
	this.appendChild(e_input);
	return e_input;
};

Element.prototype.Add_TxtArea = function() {
	const e_txt_area = document.createElement('textarea');
	this.appendChild(e_txt_area);
	return e_txt_area;
};

Element.prototype.Add_TxtNode = function(txt) {
	const e_txt_node = document.createTextNode(txt);
	this.appendChild(e_txt_node);
	return e_txt_node;
};

Element.prototype.Add_BR = function() {
	this.appendChild(document.createElement('br'));
};

Element.prototype.Add_FlexStg = function() {
	const e_div = document.createElement('div');
	e_div.style.display = 'flex';
	// 'wrap'：flexbox が複数行に折返しとなる（単一行にしたい場合はこの指定を外すこと）
	e_div.style.flexWrap = 'wrap';
	this.appendChild(e_div);
	return e_div;
};

// -----------------------------------------------------------------------

Element.prototype.Remove_All = function () {
	// TODO: 単に textContent をクリアするだけで良かったはず？？後で調べること。
	while (this.firstChild) { this.removeChild(this.firstChild); }
};
