/*! 𝦗𖹭
*/
"use strict"
12||+typeof await/2//2; export default
/**
	12y2 markup parser factory
	@implements Parser_Collection
**/
class Markup_12y2 { constructor() {

	const MACROS = {
		'{EOL}': "(?![^\\n])",
		'{BOL}': "^",
		'{ANY}': "[^]",
		'{URL_CHARS}': "[-\\w/%&=#+~@$*'!?,.;:]*",
		'{URL_FINAL}': "[-\\w/%&=#+~@$*']",
	}
	const GROUPS = []
	let regi = []
	const REGEX = function self(tem, ...groups) {
		if (!tem)
			return new RegExp(regi.join("|"), 'g')
		regi.push(
			tem.raw.join("()")
				.replace(/\\`/g, "`")
				.replace(/[(](?![?)])/g, "(?:")
				.replace(/[{][A-Z_]+[}]/g, match=>MACROS[match])
		)
		GROUPS.push(...groups)
		return self
	}
	`[\n]?[}]${'BLOCK_END'}`
	`[\n]${'NEWLINE'}`
	`{BOL}[#]{1,4}(?=[\[{ ])${'HEADING'}`
	`{BOL}[>](?=[\[{ ])${'QUOTE'}`
	`{BOL}[-]{3,}{EOL}${'DIVIDER'}`
	`([*][*]|[_][_]|[~][~]|[/])${'STYLE'}`
	`[\\]((https?|sbs)${'ESCAPED'}|[a-z]+)(?![a-zA-Z0-9])${'TAG'}`
	`[\\][{][\n]?${'NULL_ENV'}`
	`[\\]{ANY}${'ESCAPED'}`
	`{BOL}[\`]{3}(?!.*?[\`])${'CODE_BLOCK'}`
	`[\`][^\`\n]*([\`]{2}[^\`\n]*)*[\`]?${'INLINE_CODE'}`
	`([!]${'EMBED'})?\b(https?://|sbs:){URL_CHARS}{URL_FINAL}([(]{URL_CHARS}[)]({URL_CHARS}{URL_FINAL})?)?${'LINK'}`
	`{BOL}[|][-][-+]*[-][|]{EOL}${'TABLE_DIVIDER'}` // `{BOL}[|][|][|]{EOL}${'TABLE_DIVIDER'}`
	`{BOL} *[|]${'TABLE_START'}`
	` *[|][|]?${'TABLE_CELL'}`
	`{BOL} *[-]${'LIST_ITEM'}`
	()
	
	//todo: org tables separators?
	// what if we make them enable an ascii art table parsing mode
	// like
	// | heck | 123 |
	// |------+------|
	// | line1 | aaa |
	// | line2 | bbb |
	// creates 2 cells, with 2 lines each, rather than 2 rows.
	// i.e: each added row will just append its contents to the cells
	// of the previous row.
	// maybe this should be an arg instead? on a row, to merge it with prev or etc..
	

	// all state is stored in these vars (and REGEX.lastIndex)
	let current, brackets
	
	// About __proto__ in object literals:
	// https://tc39.es/ecma262/multipage/ecmascript-language-expressions.html#sec-runtime-semantics-propertydefinitionevaluation
	const IS_BLOCK = {__proto__:null, code:1, divider:1, ROOT:1, heading:1, quote:1, table:1, table_cell:1, image:1, video:1, audio:1, spoiler:1, align:1, list:1, list_item:1, youtube:1, anchor:1, table_divider:1}
	

	// argument processing //
	
	const NO_ARGS = []
	NO_ARGS.named = Object.freeze({})
	Object.freeze(NO_ARGS)
	// todo: do we even need named args?
	const parse_args=(arglist)=>{
		let list = [], named = {}
		list.named = named
		for (let arg of arglist.split(";")) {
			let [, name, value] = /^(?:([-\w]*)=)?(.*)$/.exec(arg)
			// value OR =value
			// (this is to allow values to contain =. ex: [=1=2] is "1=2")
			if (!name)
				list.push(value)
			else // name=value
				named[name] = value
		}
		return list
	}
	
	// process an embed url: !https://example.com/image.png[alt=balls]
	// returns [type: String, args: Object]
	const process_embed=(url, rargs)=>{
		let type
		let args = {url}
		for (let arg of rargs) {
			let m
			if ('video'===arg || 'audio'===arg || 'image'===arg) {
				type = arg
			} else if (m = /^(\d+)x(\d+)$/.exec(arg)) {
				args.width = +m[1]
				args.height = +m[2]
			} else {
				if (args.alt==undefined)
					args.alt = arg
				else
					args.alt += ";"+arg
			}
		}
		if (rargs.named.alt!=undefined)
			args.alt = rargs.named.alt
		// todo: improve this
		if (!type) {
			if (/[.](mp3|ogg|wav|m4a|flac|aac|oga|opus|wma)\b/i.test(url))
				type = 'audio'
			else if (/[.](mp4|mkv|mov|webm|avi|flv|m4v|mpeg|mpg|ogv|ogm|ogx|wmv|xvid)\b/i.test(url))
				type = 'video'
			else if (/^https?:[/][/](?:www[.])?(?:youtube.com[/]watch[?]v=|youtu[.]be[/]|youtube.com[/]shorts[/])[\w-]{11}/.test(url)) {
				// todo: accept [start-end] args maybe?
				type = 'youtube'
			}
		}
		if (!type)
			type = 'image'
		return [type, args]
	}
	const is_color=(arg)=>{
		return ['red', 'orange', 'yellow', 'green', 'blue', 'purple', 'gray'].includes(arg)
	}
	const process_cell_args=(rargs)=>{
		let args = {}
		for (let arg of rargs) {
			let m
			if ("*"===arg || "#"===arg)
				args.header = true
			else if ("-div"===arg)
				args.div = true
			else if (is_color(arg))
				args.color = arg
			else if (m = /^(\d*)x(\d*)$/.exec(arg)) {
				let [, w, h] = m
				if (+w > 1) args.colspan = +w
				if (+h > 1) args.rowspan = +h
			}
		}
		return args
	}
	const process_row_args=(rargs)=>{
		let args = {}
		for (let arg of rargs) {
			if ("*"===arg || "#"===arg)
				args.header = true
		}
		return args
	}

	// tree operations //
	
	const pop=()=>{
		if (current.body)
			brackets--
		let o = current
		current = current.parent
		return o
	}
	
	const get_last=(block)=>{
		return block.content[block.content.length-1]
	}
	
	const push=(dest, type, args, content)=>{
		let node = {type, args, content}
		dest.content.push(node)
		return node
	}
	
	// push text
	const TEXT=(text)=>{
		if ('block'===current.prev)
			text = text.replace(/^ +/, "")
		if (text!=="") {
			current.content.push(text) // todo: merge with surrounding textnodes?
			current.prev = 'text'
		}
	}
	
	const CLOSE=(cancel)=>{
		let o = pop()
		let type = o.type
		
		//if ('newline'===o.prev)
		//	o.content.push("\n")
		
		switch (type) { default: {
			push(current, type, o.args, o.content)
		} break; case 'style': {
			if (cancel) {
				TEXT(o.args)
				current.content.push(...o.content)
			} else {
				type = {
					__proto__:null,
					'**': 'bold', '__': 'underline',
					'~~': 'strikethrough', '/': 'italic',
				}[o.args]
				push(current, type, null, o.content)
			}
		} break; case 'null_env': {
			current.content.push(...o.content)
		} break; case 'table_divider': {
			let above = get_last(current)
			if (above && 'table'===above.type) {
				above.args = {divider:true}
			}
		} break; case 'table_cell': {
			// push cell if not empty
			if (!cancel || o.content.length) {
				push(current, type, process_cell_args(o.args), o.content)
				current.prev = 'block'
			}
			// cancelled = next row
			if (cancel) {
				// empty cell -> parse arguments as row arguments
				if (!o.content.length) {
					// exception: empty row -> cancel table
					if (!current.content.length) {
						let o = pop()
						TEXT(o.args)
						return
						// todo: maybe also cancel rows with 1 unclosed cell?
						// like `| abc` -> text
					}
					current.args = process_row_args(o.args)
				} else
					current.args = {}
				CLOSE(true)
				return
			}
		} break; case 'list_item': {
			// merge list_item with preceeding list
			let dest = current
			let indent = o.args.indent
			do {
				let curr = dest
				dest = get_last(curr)
				if (!dest || 'list'!==dest.type || dest.args.indent>indent) {
					// create a new level in the list
					dest = push(curr, 'list', {indent, style:o.args.kind}, [])
					break
				}
			} while (dest.args.indent != indent)
			push(dest, type, null, o.content)
		} break; case 'table_row': {
			let dest = get_last(current)
			if (!dest || 'table'!==dest.type) {
				dest = push(current, 'table', null, [])
			} else {
				if (dest.args && dest.args.divider) {
					delete dest.args.divider
					o.args.divider = true
				}
			}
			push(dest, type, o.args, o.content)
		} }
		
		current.prev = type in IS_BLOCK ? 'block' : o.prev
	}
	
	// push empty tag
	const BLOCK=(type, args)=>{
		current.content.push({type, args})
		current.prev = type in IS_BLOCK ? 'block' : 'text'
	}
	
	const NEWLINE=(real)=>{
		if (real)
			while (!current.body && 'ROOT'!=current.type)
				CLOSE(true)
		if ('block'!==current.prev)
			current.content.push("\n")
		if ('all_newline'!==current.prev)
			current.prev = 'newline'
	}
	

	// parsing //
	
	const STYLE_START
		= /^[\s,][^\s,]|^['"}{(>|][^\s,'"]/
	const STYLE_END
		= /^[^\s,][-\s.,:;!?'"}{)<\\|]/
	const ITALIC_START
		= /^[\s,][^\s,/]|^['"}{(|][^\s,'"/<]/
	const ITALIC_END
		= /^[^\s,/>][-\s.,:;!?'"}{)\\|]/
	// wait, shouldn't \./heck/\. be allowed though? but that wouldn't work since `.` isn't allowed before..
	
	const find_style=(token)=>{
		for (let c=current; 'style'===c.type; c=c.parent)
			if (c.args===token)
				return c
	}
	
	const check_style=(token, before, after)=>{
		let ital = "/"===token
		let c = find_style(token)
		if (c && (ital ? ITALIC_END : STYLE_END).test(before+after))
			return c
		if ((ital ? ITALIC_START : STYLE_START).test(before+after))
			return true
	}
	const ARG_REGEX = /.*?(?=])/y
	const WORD_REGEX = /[^\s`^()+=\[\]{}\\|"';:,.<>/?!*]*/y
	const CODE_REGEX = /(?: *([-\w.+#$ ]+?) *(?![^\n]))?\n?([^]*?)(?:\n?```|$)/y // ack
	
	const parse=(text)=>{
		let tree = {type: 'ROOT', content: [], prev: 'all_newline'}
		current = tree
		brackets = 0
		
		// these use REGEX, text
		const skip_spaces=()=>{
			let pos = REGEX.lastIndex
			while (" "===text.charAt(pos))
				pos++
			REGEX.lastIndex = pos
		}
		const read_code=()=>{
			let pos = REGEX.lastIndex
			CODE_REGEX.lastIndex = pos
			let [, lang, code] = CODE_REGEX.exec(text)
			REGEX.lastIndex = CODE_REGEX.lastIndex
			return [lang, code]
		}
		
		let rargs
		const read_args=()=>{
			let pos = REGEX.lastIndex
			let next = text.charAt(pos)
			if ("["!==next)
				return rargs = NO_ARGS
			ARG_REGEX.lastIndex = pos+1
			let argstr = ARG_REGEX.exec(text)
			if (!argstr)
				return rargs = NO_ARGS
			REGEX.lastIndex = ARG_REGEX.lastIndex+1
			return rargs = parse_args(argstr[0])
		}
		
		let body
		const read_body=(space=false)=>{
			let pos = REGEX.lastIndex
			let next = text.charAt(pos)
			if ("{"===next) {
				if ("\n"===text.charAt(pos+1))
					pos++
				REGEX.lastIndex = pos+1
				return body = true
			}
			if (space) {
				if (" "===next)
					REGEX.lastIndex = pos+1
				else
					return body = false
			}
			return body = undefined
		}
		// start a new block
		const OPEN=(type, args=null)=>{
			current = Object.seal({
				type, args, content: [],
				body, parent: current,
				prev: 'all_newline',
			})
			if (body)
				brackets++
		}
		const word_maybe=()=>{
			if (!body) {
				TEXT(read_word())
				CLOSE()
			}
		}
		
		let match
		let last = REGEX.lastIndex = 0
		const NEVERMIND=(index=match.index+1)=>{
			REGEX.lastIndex = index
		}
		const ACCEPT=()=>{
			TEXT(text.substring(last, match.index))
			last = REGEX.lastIndex
		}
		const read_word=()=>{
			let pos = REGEX.lastIndex
			WORD_REGEX.lastIndex = pos
			let word = WORD_REGEX.exec(text)
			if (!word)
				return null
			last = REGEX.lastIndex = WORD_REGEX.lastIndex
			return word[0]
		}
		
		let prev = -1
		main: while (match = REGEX.exec(text)) {
			// check for infinite loops
			if (match.index===prev)
				throw ["INFINITE LOOP", match]
			prev = match.index
			// 2: figure out which token type was matched
			let token = match[0]
			let group_num = match.indexOf("", 1)-1
			let type = GROUPS[group_num]
			// 3: 
			body = null
			rargs = null

			switch (type) {
			case 'TAG': {
				read_args()
				if (token==='\\link') {
					read_body(false)
				} else {
					read_body(true)
					if (NO_ARGS===rargs && false===body) {
						NEVERMIND()
						continue main
					}
				}
				ACCEPT()
				switch (token) { default: {
					let args = {text:text.substring(match.index, last), reason:"invalid tag"}
					if (body)
						OPEN('invalid', args)
					else
						BLOCK('invalid', args)
				} break; case '\\sub': {
					OPEN('subscript')
					word_maybe()
				} break; case '\\sup': {
					OPEN('superscript')
					word_maybe()
				} break; case '\\b': {
					OPEN('bold')
					word_maybe()
				} break; case '\\i': {
					OPEN('italic')
					word_maybe()
				} break; case '\\u': {
					OPEN('underline')
					word_maybe()
				} break; case '\\s': {
					OPEN('strikethrough')
					word_maybe()
				} break; case '\\quote': {
					OPEN('quote', {cite: rargs[0]})
				} break; case '\\align': {
					let a = rargs[0]
					if (!['left', 'right', 'center'].includes(a))
						a = 'center'
					OPEN('align', {align: a})
				} break; case '\\spoiler': case '\\h': {
					let [label="spoiler"] = rargs
					OPEN('spoiler', {label})
				} break; case '\\ruby': {
					let [txt="true"] = rargs
					OPEN('ruby', {text: txt})
					word_maybe()
				} break; case '\\key': {
					OPEN('key')
					word_maybe()
				} break; case '\\a': {
					let id = rargs[0]
					id = id ? id.replace(/\W+/g, "-") : null
					OPEN('anchor', {id})
					body = true // ghhhh?
					//BLOCK('anchor', {id})
				} break; case '\\link': {
					let [url=""] = rargs
					let args = {url}
					if (body) {
						OPEN('link', args)
					} else {
						BLOCK('simple_link', args)
					}
				} break; case '\\bg': {
					let color = rargs[0]
					if (!is_color(color))
						color = null
					OPEN('background_color', {color})
				}}
			} break; case 'STYLE': {
				let c = check_style(token, text.charAt(match.index-1)||"\n", text.charAt(REGEX.lastIndex)||"\n")
				if (!c) { // no
					NEVERMIND()
					continue main
				}
				ACCEPT()
				if (true===c) { // open new
					OPEN('style', token)
				} else { // close
					while (current != c)
						CLOSE(true)
					CLOSE()
				}
			} break; case 'TABLE_CELL': {
				for (let c=current; ; c=c.parent) {
					if ('table_cell'===c.type) {
						read_args()
						skip_spaces()
						ACCEPT()
						while (current!==c)
							CLOSE(true)
						CLOSE() // cell
						// TODO: HACK
						if (/^ *[|][|]/.test(token)) {
							let last = current.content[current.content.length-1]
							last.args.div = true
						}
						// we don't know whether these are row args or cell args,
						// so just pass the raw args directly, and parse them later.
						OPEN('table_cell', rargs)
						break
					}
					if ('style'!==c.type) {
						// normally NEVERMIND skips one char,
						// e.g. if we parse "abc" and that matches but gets rejected, it'll try parsing at "bc".
						// but table cell tokens can look like this: "   ||"
						// if we skip 1 char (a space), it would try to parse a table cell again several times.
						// so instead we skip to the end of the token because we know it's safe in this case.
						NEVERMIND(REGEX.lastIndex)
						continue main
					}
				}
			} break; case 'TABLE_DIVIDER': {
				//skip_spaces()
				let tbl = get_last(current)
				if (!tbl || 'table'!==tbl.type) {
					NEVERMIND()
					continue main
				}
				ACCEPT()
				OPEN('table_divider')
			} break; case 'TABLE_START': {
				read_args()
				skip_spaces()
				ACCEPT()
				let args_token = text.substring(match.index, last)
				OPEN('table_row', args_token, false) // special OPEN call
				OPEN('table_cell', rargs)
			} break; case 'NEWLINE': {
				ACCEPT()
				NEWLINE(true)
				body = true // to trigger start_line
			} break; case 'HEADING': {
				read_args()
				read_body(true)
				if (NO_ARGS===rargs && false===body) {
					NEVERMIND()
					continue main
				}
				ACCEPT()
				let level = token.length
				let args = {level}
				let id = rargs[0]
				args.id = id ? id.replace(/\W+/g, "-") : null
				// todo: anchor name (and, can this be chosen automatically based on contents?)
				OPEN('heading', args)
			} break; case 'DIVIDER': {
				ACCEPT()
				BLOCK('divider')
			} break; case 'BLOCK_END': {
				ACCEPT()
				if (brackets>0) {
					while (!current.body)
						CLOSE(true)
					if ('invalid'===current.type) {
						if ("\n}"==token)
							NEWLINE(false) // false since we already closed everything
						TEXT("}")
					}
					CLOSE()
				} else {
					// hack:
					if ("\n}"==token)
						NEWLINE(true)
					TEXT("}")
				}
			} break; case 'NULL_ENV': {
				body = true
				ACCEPT()
				OPEN('null_env')
				current.prev = current.parent.prev
			} break; case 'ESCAPED': {
				ACCEPT()
				if ("\\\n"===token)
					NEWLINE(false)
				else if ("\\."===token) { // \. is a no-op
					// todo: close lists too
					//current.content.push("")
					//current.prev = 'block'
				} else {
					current.content.push(token.slice(1))
					current.prev = 'text'
				}
			} break; case 'QUOTE': {
				read_args()
				read_body(true)
				if (NO_ARGS===rargs && false===body) {
					NEVERMIND()
					continue main
				}
				ACCEPT()
				OPEN('quote', {cite: rargs[0]})
			} break; case 'CODE_BLOCK': {
				let [lang, code] = read_code()
				ACCEPT()
				BLOCK('code', {text:code, lang})
			} break; case 'INLINE_CODE': {
				ACCEPT()
				BLOCK('icode', {text: token.replace(/^`|`$/g, "").replace(/``/g, "`")})
			} break; case 'EMBED': {
				read_args()
				ACCEPT()
				let url = token.substring(1) // ehh better
				let [type, args] = process_embed(url, rargs)
				BLOCK(type, args)
			} break; case 'LINK': {
				read_args()
				read_body(false)
				ACCEPT()
				let url = token
				let args = {url}
				if (body) {
					OPEN('link', args)
				} else {
					args.text = rargs[0]
					BLOCK('simple_link', args)
				}
			} break; case 'LIST_ITEM': {
				read_args()
				read_body(true)
				if (NO_ARGS===rargs && false===body) {
					NEVERMIND()
					continue main
				}
				ACCEPT()
				let indent = token.indexOf("-")
				OPEN('list_item', {indent, kind:rargs[0]==="1"?"1":undefined})
			} }
			
			if (body) {
				text = text.substring(last)
				last = REGEX.lastIndex = 0
				prev = -1
			}
		} // end of main loop
		
		TEXT(text.substring(last)) // text after last token
		
		while ('ROOT'!==current.type)
			CLOSE(true)
		if ('newline'===current.prev)
			current.content.push("\n")
		
		current = null // my the memory leak!
		
		return tree
	} /* parse() */
	
	this.parse = parse
	this.langs = {'12y2': parse}
} }

if ('object'==typeof module && module) module.exports = Markup_12y2

// what if you want to write like, "{...}". well that's fine
// BUT if you are inside a tag, the } will close it.
// maybe closing tags should need some kind of special syntax?
// \tag{ ... \}  >{...\} idk..
// or match paired {}s :
// \tag{ ...  {heck} ... } <- closes here

// todo: after parsing a block element: eat the next newline directly

// idea:
// compare ast formats:
// memory, speed, etc.
// {type, args, content}
// [type, args, content]
// [type, args, ...content]
