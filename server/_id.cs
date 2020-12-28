namespace md_svr
{
	internal enum ID : byte
	{
		Undefined = 0,

		// ----------------------------------------------------------
		// 上り、下り両用のコード
		Directory_Names = 1,
		File_Names = 2,
		Text = 3,
		End = 4,

		// ----------------------------------------------------------
		// Param に Succeed or Fail を付加する
		// クライアントが Lexed_MD を受信したときに、エラーがあった場合、ページ先頭に警告を表示したいため
		Lexed_MD = 10,
		ERR_OVERFLOW = 11,
		// このマーク以降に、テキスト情報（最大文字数 EN_MAX_ERR_Text）が書き込まれている
		// ERR_Report のマークの後に ID.Div、ID.Text が続く
		// 特に、埋め込みメッセージがある場合は、ID.Div、ID.Text が２つ続く
		ERR_Report = 12,
		ERR_on_Simplify = 13,

		BR = 20,  // 特殊扱い

		// ----------------------------------------------------------
		// 行頭ブロック用
		Div = 30,
		Div_Head = 31,  // param に 1 - 6 が指定される
		Div_Code = 35,
		Div_Quote = 36,

		// ----------------------------------------------------------
		// インライン用

	}

	enum Param : byte
	{
		Succeeded = 1,
		Failed = 2,
	}

	static class Chr
	{
		public const int PCS_Tab = 3;
		public const char TAB = (char)0x09;

		public const char LF = (char)0x0a;
		public const char CR = (char)0x0d;
		public const uint CRLF = (uint)0x000a_000d;

		public const char SP = (char)0x20;
		public const char SP_ZEN = (char)0x3000;
	}
}
