'use strict';

import * as Lexer from "./Lexer.js";

const lexer_run = (src) => {
	const ret = Lexer.g_lexer.Run(src);

	if (ret == Lexer.RET_Lexer_OK)
	{ console.log('+++ 字句解析　成功'); }
	else
	{ console.log('!!! 字句解析　失敗'); }
};

Lexer_Run = lexer_run;
