// 📤📥doc
// todo: after parsing a block element: eat the next newline directly

/**
	12y2 parser
	@implements Langs_Mixin
	@hideconstructor
*/
class Markup_12y2 { constructor() {
	// all state is stored in these vars (and REGEX.lastIndex)
	let current, brackets
	
	// idea: maybe instead of this separate parse step, we should just do something like
	// go back to using ex: /^><args>?[{ ]/
	// have either
	// - custom post-processing regex for each token (ex /[\\](\w+)(<args>)?({)?/ )
	// - include these extra groups in the main regex, remove the () group, and find a replacement for the () indexOf("") system
	

	
	const MAP = x=>Object.freeze(Object.create(null, Object.getOwnPropertyDescriptors(x)))
	
	const CAN_CANCEL = MAP({style:1, table_cell:1})
	// elements which can survive an eol (without a body)
	const SURVIVE_EOL = MAP({ROOT:1, table_cell:1})
	const IS_BLOCK = MAP({code:1, divider:1, ROOT:1, heading:1, quote:1, table:1, table_cell:1, image:1, video:1, audio:1, spoiler:1, align:1, list:1, list_item:1, error:1, youtube:1})
	
	// argtype
	const ARGS_NORMAL   = /(?:\[([^\]\n]*)\])?({)?/y      // [...]?{?
	const ARGS_WORD     = /(?:\[([^\]\n]*)\])?({| (\w*) ?)/y // [...]?{ or [...]? <word> // todo: more complex rule for word parsing
	const ARGS_LINE     = /(?:\[([^\]\n]*)\])?(?:({)| ?)/y      // [...]?{? probably dont need this, we can strip space after { in all cases instead.
	const ARGS_HEADING  = /(?:\[([^\]\n]*)\])?(?:({)| )/y // [...]?( |{)
	const ARGS_BODYLESS = /(?:\[([^\]\n]*)\])?/y          // [...]?
	const ARGS_TABLE    = /(?:\[([^\]\n]*)\])? */y        // [...]? *
	
	// when an unknown \tag is encountered, we create a block
	// rather than just ignoring it, so in the future,
	// we can add a new tag without changing the parsing (much)
	const ENV_INVALID = {
		argtype:ARGS_NORMAL, do(tag, rargs, body) {
			if (body)
				return OPEN('invalid', tag, {text: tag, reason:"invalid tag"}, body)
			else
				return TAG('invalid', {text: tag, reason:"invalid tag"})
		}
	}
	
	function arg0(rargs, def) {
		if (rargs.length<1)
			return def
		return rargs[0]
	}
	
	function simple_word_tag(name) {
		// nnnnn closure
		return {argtype:ARGS_WORD, do(tag, rargs, body) {
			return OPEN(name, tag, null, body)
		}}
	}
	
	const ENVS = MAP({
		sub: simple_word_tag('subscript'),
		sup: simple_word_tag('superscript'),
		b: simple_word_tag('bold'),
		i: simple_word_tag('italic'),
		u: simple_word_tag('underline'),
		s: simple_word_tag('strikethrough'),
		quote: {argtype:ARGS_LINE, do(tag, rargs, body) {
			// todo: this feels very repetitive...
			return OPEN('quote', tag, {cite: rargs[0]}, body)
		}},
		align: {argtype:ARGS_LINE, do(tag, rargs, body) {
			let a = rargs[0]
			if (!(a=='left' || a=='right' || a=='center'))
				a = 'center'
			return OPEN('align', tag, {align: a}, body)
		}},
		spoiler: {argtype:ARGS_LINE, do(tag, rargs, body) {
			let label = arg0(rargs, "spoiler")
			return OPEN('spoiler', tag, {label}, body)
		}},
		ruby: {argtype:ARGS_WORD, do(tag, rargs, body) {
			let text = arg0(rargs, "true")
			return OPEN('ruby', tag, {text}, body)
		}},
		key: {argtype:ARGS_WORD, do(tag, rargs, body) {
			return OPEN('key', tag, null, body)
		}},
	})
	
	/* NOTE:
		/^/ matches after a <newline> or <env> token
		/$/ matches end of string
		/(?![^\n])/ matches end of line
		/()/ empty tags are used to mark token types
	*/
	// ⚠ The order of these is important!
	const [REGEX, GROUPS] = process_def([[
		// 💎 NEWLINE 💎
		/\n/,
		{newline:true, do(tag) {
			while (!current.body && !SURVIVE_EOL[current.type])
				CLOSE(true)
			return NEWLINE()
		}},
	],[// 💎 HEADING 💎
		/^#{1,4}/,
		{argtype:ARGS_HEADING, do(tag, rargs, body, base) {
			let level = base.length
			let args = {level}
			// todo: anchor name (and, can this be chosen automatically based on contents?)
			return OPEN('heading', tag, args, body)
		}},
	],[// 💎 DIVIDER 💎
		/^---+(?![^\n])/,
		{do(tag) {
			return TAG('divider')
		}},
	],[// 💎💎 STYLE
		/(?:[*][*]|__|~~|[/])(?=\w()|)/, //todo: improve start/end detect
		// 💎 STYLE START 💎
		{do(tag) {
			return OPEN('style', tag)
		}},
		// 💎 STYLE END 💎
		{do(tag) {
			// todo: should be checking for WEAK here?
			while (current.type=='style') { 
				if (current.tag == tag) { // found opening
					current.type = {
						"**": 'bold',
						"__": 'underline',
						"~~": 'strikethrough',
						"/": 'italic',
					}[current.tag]
					return CLOSE()
				}
				CLOSE(true) // different style (kill)
			}
			return TEXT(tag)
		}},
	],[// 💎 ENV 💎 (handled separately)
		/[\\]\w+/,
		false,
	],[// 💎 BLOCK END 💎
		// todo: outside the end of a block/table, 
		// eat whitespace + newline ?
		// ex \spoiler{abc}  <spaces> ← those
		// also inside the {} of course,
		//[/{/, {token:''}], // maybe
		/}/,
		{do(tag) {
			if (brackets<=0)
				return TEXT(tag)
			// only runs if at least 1 element has a body, so this won't fail:
			while (!current.body)
				CLOSE(true)
			if (current.type=='invalid')
				TEXT("}")
			return CLOSE()
		}},
	],[// 💎 NULL ENV 💎 (maybe can be in the envs table now? todo)
		/[\\]{/,
		{body:true, do(tag) {
			return OPEN('null_env', tag, null, true)
		}},
	],[// 💎 ESCAPED CHAR 💎
		/[\\][^]/, //todo: match surrogate pairs
		{do(tag) {
			if (tag=="\\\n")
				return NEWLINE()
			return TEXT(tag.substr(1))
		}},
	],[// 💎 QUOTE 💎
		/^>/,
		{argtype:ARGS_HEADING, do(tag, rargs, body) {
			return OPEN('quote', tag, {cite: rargs[0]}, body)
		}},
	],[// 💎 CODE BLOCK 💎
		/^```[^]*?(?:```|$)/,
		{do(tag) {
			let [, lang, text] = /^```(?: *([-\w.+#$ ]+?)? *(?:\n|$))?([^]*?)(?:```)?$/g.exec(tag)// hack...
			// idea: strip leading indent from code?
			return TAG('code', {text, lang})
		}},
	],[// 💎 INLINE CODE 💎
		/`[^`\n]+`?/,
		{do(tag) {
			return TAG('icode', {text: tag.replace(/^`|`$/g,"")})
		}},
	],[// 💎💎 URL
		/(?:!())?(?:https?:[/][/]|sbs:)[-\w./%?&=#+~@:$*',;!)(]*[-\w/%&=#+~@$*';)(]/,
		// 💎 EMBED 💎
		{argtype:ARGS_BODYLESS, do(tag, rargs, body, base) {
			let url = base.substr(1) // ehh better
			let [type, yt] = embed_type(rargs, url)
			if (type=='youtube')
				return TAG('youtube', yt)
			let args = {
				url: url,
				alt: rargs.named.alt,
			}
			if (type=='image' || type=='video') {
				for (let arg of rargs) {
					let m
					if (m = /^(\d+)x(\d+)$/.exec(arg)) {
						args.width = +m[1]
						args.height = +m[2]
					}
				}
			}
			return TAG(type, args)
		}},
		// 💎 LINK 💎
		{argtype:ARGS_NORMAL, do(tag, rargs, body, base) {
			let url = base
			let args = {url}
			if (body)
				return OPEN('link', tag, args, true)
			args.text = arg0(rargs, url)
			return TAG('simple_link', args)
		}},
	],[// 💎 TABLE - NEXT ROW 💎
		/ *[|] *\n[|]/,
		{argtype:ARGS_TABLE, do(tag, rargs, body) {
			kill_weak()
			if (current.type!='table_cell')
				return TEXT(tag)
			let args = table_args(rargs)
			return (
				CLOSE(), // cell
				CLOSE(), // row
				OPEN('table_row', ""),
				OPEN('table_cell', tag.replace(/^ *\n/, ""), args, body))
		}},
	],[// 💎 TABLE - END 💎
		/ *[|] *(?![^\n])/,
		{do(tag) {
			kill_weak()
			if (current.type!='table_cell')
				return TEXT(tag) // todo: wait, if this happens, we just killed all those blocks even though this tag isn't valid ??
			return (
				CLOSE(),
				CLOSE(),
				CLOSE())
		}},
	],[// 💎 TABLE - START 💎
		/^ *[|]/,
		{argtype:ARGS_TABLE, do(tag, rargs, body) {
			let args = table_args(rargs)
			return (
				OPEN('table', ""),
				OPEN('table_row', ""),
				OPEN('table_cell', tag, args, body))
		}},
	],[// 💎 TABLE - NEXT CELL 💎
		/ *[|]/,
		{argtype:ARGS_TABLE, do(tag, rargs, body) {
			kill_weak()
			if (current.type!='table_cell')
				return TEXT(tag)
			let args = table_args(rargs)
			return (
				CLOSE(), // cell
				OPEN('table_cell', tag.replace(/^ *[|]/, ""), args, body))
		}},
	]]) // todo: we can probably merge a few table types, to save on match count / complexity..
	// and, maybe instead of using ^ and truncating the string on newline,
	// we can just filter the match results and retry if a match is in an invalid location (similar to how results are rejected if arg parsing fails)
	
	//[/^ *- /, 'list'], TODO
	

	
	function process_def(table) {
		let regi = []
		let groups = []
		for (let [regex, ...matches] of table) {
			regi.push(regex.source+"()")
			groups.push(...matches)
		}
		let r = new RegExp(regi.join("|"), 'g')
		return [r, groups]
	}
	
	const null_args = []
	null_args.named = Object.freeze({})
	Object.freeze(null_args)
	// todo: do we even need named args?
	function parse_args(arglist) {
		// note: checks undefined AND "" (\tag AND \tag[])
		if (!arglist) 
			return null_args
		
		let list = []
		list.named = {}
		for (let arg of arglist.split(";")) {
			let [, name, value] = /^(?:([^=]*)=)?(.*)$/.exec(arg)
			// value OR =value
			// (this is to allow values to contain =. ex: [=1=2] is "1=2")
			if (!name)
				list.push(value)
			else // name=value
				list.named[name] = value
		}
		return list
	}
	function embed_type(rargs, url) {
		let type
		for (let arg of rargs)
			if (arg=='video' || arg=='audio' || arg=='image')
				type = arg
		// todo: improve this
		if (type)
			return [type]
		if (/[.](mp3|ogg|wav|m4a)(?!\w)/i.test(url))
			return ['audio']
		if (/[.](mp4|mkv|mov)(?!\w)/i.test(url))
			return ['video']
		// youtube
		let m = /^https?:[/][/](?:www[.])?(?:youtube.com[/]watch[?]v=|youtu[.]be[/])([\w-]{11,})(?:[&?](.*))?$/.exec(url)
		if (m) {
			let [, id, query] = m
			// todo: use query here to extract start/end times
			// also, accept [start-end] args maybe?
			return ['youtube', {id, url}]
		}
		// default
		return ['image']
	}
	function table_args(rargs) {
		let ret = {}
		for (let arg of rargs) {
			let m
			if (arg=="*")
				ret.header = true
			else if (['red','orange','yellow','green','blue','purple','gray'].includes(arg))
				ret.color = arg
			else if (m = /^(\d*)x(\d*)$/.exec(arg)) {
				let [, w, h] = m
				if (+w > 1) ret.colspan = +w
				if (+h > 1) ret.rowspan = +h
			}
		}
		return ret
	}
	
	// start a new block
	function OPEN(type, tag, args, body) {
		// todo: anything with a body doesn't need a tag, i think
		// since body items can never be cancelled.
		// so perhaps we can specify the body flag by setting tag = true
		current = {type, tag, content: [], parent: current, prev: 'all_newline'}
		if (body) {
			brackets++
			current.body = true
		}
		if (args)
			current.args = args
	}
	// move up
	function pop() {
		if (current.body)
			brackets--
		let o = current
		current = current.parent
		return o
	}
	// sketchy...
	function merge(content, prev, tag) {
		if (tag)
			current.content.push(tag)
		else if (current.prev=='block' && content[0]==="\n")
			content.shift() // strip newline
		
		current.content.push(...content)
		current.prev = prev
	}
	// complete current block
	function CLOSE(cancel) {
		// push the block + move up
		let o = pop()
		
		if (cancel && !o.body && CAN_CANCEL[o.type]) {
			if (o.type=='table_cell') {
				// close table row (cancel if empty)
				current.content.length ? CLOSE() : pop()
				// close table (cancel if empty)
				current.content.length ? CLOSE() : TEXT(pop().tag)
			}
			merge(o.content, o.prev, o.tag)
		} else if (o.type=='null_env') {
			merge(o.content, o.prev)
		} else {
			// otherwise, we have a normal block:
			if (o.prev=='newline')
				o.content.push("\n")
			delete o.parent // remove cyclical reference before adding to tree. TODO: for some reason this line causes the code to run like 20% slower lol
			current.content.push(o)
			current.prev = IS_BLOCK[o.type] ? 'block' : o.prev
		}
	}
	// push text
	function TEXT(text) {
		if (text) {
			current.content.push(text) // todo: merge with surrounding textnodes?
			current.prev = 'text'
		}
	}
	// push empty tag
	function TAG(type, args) {
		current.content.push({type, args})
		current.prev = IS_BLOCK[type] ? 'block' : 'text'
	}
	function NEWLINE() {
		if (current.prev != 'block')
			current.content.push("\n")
		if (current.prev != 'all_newline')
			current.prev = 'newline'
	}
	function kill_weak() {
		while (current.type=='style')
			CLOSE(true)
	}
	
	function parse(text) {
		let tree = {type:'ROOT', tag:"", content:[], prev:'all_newline'}
		current = tree
		brackets = 0
		
		let last = REGEX.lastIndex = 0
		for (let match; match=REGEX.exec(text); ) {
			// text before token
			TEXT(text.substring(last, match.index))
			// get token
			//for (; match[group_num]===undefined; group_num++)
			//	;
			let group_num = match.indexOf("", 1)-1
			let thing = GROUPS[group_num]
			// is a \tag
			if (thing===false) {
				let name = match[0].substr(1)
				thing = ENVS[name] || ENV_INVALID
			}
			// parse args and {
			let body
			let argregex = thing.argtype
			if (argregex) {
				argregex.lastIndex = REGEX.lastIndex
				let argmatch = argregex.exec(text)
				if (!argmatch) { // INVALID! skip 1 char
					REGEX.lastIndex = match.index+1
					last = match.index
					continue
				}
				body = argmatch[2]
				thing.do(match[0]+argmatch[0], parse_args(argmatch[1]), body, match[0])
				if (argmatch[3]!==undefined) {
					let text = argmatch[3]
					TEXT(text.replace(/\\([^])/g,"$1")) // todo: i wonder if we could pass indexes to TEXT, and have it automatically extract from the input string, only when necessary. i.e. 2 consecutive text tokens are pulled with a single .substring()
					CLOSE()
					body = false
				}
				last = REGEX.lastIndex = argregex.lastIndex
			} else {
				body = thing.body
				thing.do(match[0])
				last = REGEX.lastIndex
			}
			// "start of line"
			if (body || thing.newline) {
				text = text.substring(last)
				last = REGEX.lastIndex = 0
			}
		}
		TEXT(text.substring(last)) // text after last token
		
		while (current.type!='ROOT')
			CLOSE(true)
		if (current.prev=='newline') // todo: this is repeated
			current.content.push("\n")
		
		return tree // technically we could return `current` here and get rid of `tree` entirely
	}
	
	/**
		parser
		@instance
		@type {Parser_Function}
	*/
	this.parse = parse
	/**
		@instance
		@type {Object}
		@property {Parser_Function} 12y2 - same as .parse
	*/
	this.langs = {'12y2': parse}
	
	// what if you want to write like, "{...}". well that's fine
	// BUT if you are inside a tag, the } will close it.
	// maybe closing tags should need some kind of special syntax?
	// \tag{ ... \}  >{...\} idk..
	// or match paired {}s :  
	// \tag{ ...  {heck} ... } <- closes here
	
}}

if ('object'==typeof module && module) module.exports = Markup_12y2
