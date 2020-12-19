
export const LEX_text = 1
export const LEX_end = 2
export const LEX_ERR = 3  // これは、end マークも兼ねている

export const LEX_head_1 = 51
export const LEX_head_6 = 56

export const LEX_code_blk = 60
export const LEX_code_inline = 61
export const LEX_code_end = 62



export const DBG_ShowToken = (ary_token) => {
	const ary_str_token = [];

	for (const type of ary_token) {

		switch (type) {
		case LEX_text: ary_str_token.push('text'); continue;
		case LEX_end: ary_str_token.push('end'); continue;
		case LEX_ERR: ary_str_token.push('ERR'); continue;

		case LEX_code_blk: ary_str_token.push('code_blk'); continue;
		case LEX_code_inline: ary_str_token.push('code_inline'); continue;
		case LEX_code_end: ary_str_token.push('code_end'); continue;
		}

		if (LEX_head_1 <= type && type <= LEX_head_6) {
			ary_str_token.push('head_' + (type - LEX_head_1 + 1));
			continue;
		}

		ary_str_token.push('!!! UNKNOWN');
	}

	console.log(ary_str_token);
};
