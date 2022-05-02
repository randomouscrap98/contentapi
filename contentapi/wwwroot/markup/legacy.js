/**
	legacy parser collection
	@implements Langs_Mixin
	@hideconstructor
*/
class Markup_Legacy { constructor() {
	"use strict"
	
	/**
		@instance
		@type {Object}
		@property {Parser_Function} 12y - 12y parser
		@property {Parser_Function} bbcode - bbcode parser
		@property {Parser_Function} plaintext - plaintext parser/autolinker
	*/
	this.langs = {}
	
	const MAP = x=>Object.freeze(Object.create(null, Object.getOwnPropertyDescriptors(x)))
	
	let BLOCKS = MAP({
		divider:1, code:1, audio:1, video:1, youtube:1,
		heading:1, quote:1, list:1, list_item:1,
		table:1, table_row:1, image:1, error:1,
		align:1, spoiler:1
	})
	
	function convert_cell_args(props, h) {
		let args = {
			header: props.h || h,
			colspan: props.cs, // TODO: validate
			rowspan: props.rs,
			align: props.align,
			color: props.c,
		}
		if (props.c && props.c[0]=='#')
			args.truecolor = props.c
		return args
	}
	
	/***********
	 ** STATE **
	 ***********/
	let c, i, code
	let skipNextLineBreak
	let textBuffer
	let curr, tree
	let openBlocks
	let stack
	
	let startOfLine
	let leadingSpaces
	function lineStart() {
		startOfLine = true
		leadingSpaces = 0
	}
	function scan() {	
		if (c == "\n" || !c)
			lineStart()
		else if (c != " ")
			startOfLine = false
		else if (startOfLine)
			leadingSpaces++
		i++
		c = code.charAt(i)
	}
	function stack_top() {
		return stack[stack.length-1]	
	}
	
	function init(text) {
		code = text
		openBlocks = 0
		leadingSpaces = 0
		startOfLine = true
		skipNextLineBreak = false
		textBuffer = ""
		tree = curr = {type:'ROOT', content:[]}
		stack = [{node:curr, type:'ROOT'}]
		restore(0)
	}
	// move to pos
	function restore(pos) {
		i = pos-1
		scan()
	}
	
	//try to read a char
	function eatChar(chr) {
		if (c == chr) {
			scan()
			return true
		}
	}
	
	function matchNext(str) {
		return code.substr(i, str.length) == str
	}
	
	// read a url
	// if `allow` is true, url is only ended by end of file or ]] or ][ (TODO)
	function readUrl(allow) {
		let start = i
		let depth = 0
		if (allow)
			while (c) {
				if (eatChar("[")) {
					depth++
				} else if (c=="]") {
					depth--
					if (depth<0)
						break
					scan()
				} else
					scan()
			}
		else {
			while (c) {
				if (/[-\w\$\.+!*',;/\?:@=&#%~]/.test(c)) {
					scan()
				} else if (eatChar("(")) {
					depth++
				} else if (c==")") {
					depth--
					if (depth < 0)
						break
					scan()
				} else
					break
			}
			let last = code[i-1]
			if (/[,\.?!:]/.test(last)) {
				i-=2
				scan()
			}
		}
		return code.substring(start, i)
	}
	
	/***********
	 ** stack **
	 ***********/
	function stackContains(type) {
		for (let i=0; i<stack.length; i++) {
			if (stack[i].type == type)
				return true
		}
		return false
	}
	function top_is(type) {
		let top = stack_top()
		return top && top.type == type
	}
	
	/****************
	 ** outputting **
	 ****************/
	function endBlock() {
		flushText()
		let item = stack.pop()
		if (item.isBlock)
			skipNextLineBreak = true
		
		if (stack.length) {
			let i=stack.length-1
			// this skips {} fake nodes
			// it will always find at least the root <div> element I hope
			while (!stack[i].node)
				i--
			curr = stack[i].node
			openBlocks--
		} else {
			curr = null
		}
	}
	
	// output contents of text buffer
	function flushText() {
		if (textBuffer) {
			curr.content.push(textBuffer)
			textBuffer = ""
		}
	}
	
	// add linebreak to output
	// todo: skipping linebreaks should skip / *\n? */ (spaces before/after!)
	// so like [h1]test[/h1] [h2]test[/h2]
	// no extra linebreak there
	function addLineBreak() {
		if (skipNextLineBreak)
			skipNextLineBreak = false
		else
			addText("\n")
		//add_block(true)
	}
	
	// add text to output (buffered)
	function addText(text) {
		if (text) {
			textBuffer += text
			skipNextLineBreak = false
		}
	}
	
	// call at end of parsing to flush output
	function endAll() {
		flushText()
		while (stack.length)
			endBlock()
	}
	
	function add_block(type, args) {
		flushText()
		curr.content.push({type, args})
		skipNextLineBreak = BLOCKS[type]
	}
	
	function start_block(type, args, data) {
		if (type) {
			let node = {type, args, content:[]}
			data.type = type
			openBlocks++
			if (openBlocks > 10)
				throw "too deep nestted blocks"
			data.node = node
			if (BLOCKS[type]) {
				data.isBlock = true
				skipNextLineBreak = true
			}
			flushText()
			curr.content.push(node)
			curr = node
		}
		stack.push(data)
		return data
	}
	
	// check for /\b(http://|https://|sbs:)/ basically
	function isUrlStart() {
		if (code[i-1] && /\w/.test(code[i-1]))
			return false
		return matchNext("http://") || matchNext("https://") || matchNext("sbs:")
	}
	
	this.langs['12y'] = function(codeInput) {
		init(codeInput)
		curr.lang = '12y'
		if (!codeInput)
			return tree
		
		while (c) {
			if (eatChar("\n")) {
				endLine()
				//==========
				// \ escape
			} else if (eatChar("\\")) {
				/*				if (c == "\n") {
								add_block(true)
								} else*/
				addText(c)
				scan()
				//===============
				// { group start (why did I call these "groups"?)
			} else if (c == "{") {
				readEnv()
				//=============
				// } group end
			} else if (eatChar("}")) {
				if (stackContains(null)) {
					closeAll(false)
				} else {
					addText("}")
				}
				//================
				// * heading/bold
			} else if (c == "*") {
				if (startOfLine && (code[i+1] == "*" || code[i+1] == " ")) {
					let headingLevel = 0
					while (eatChar("*"))
						headingLevel++
					if (headingLevel > 3)
						headingLevel = 3
					
					if (eatChar(" "))
						start_block('heading', {level:headingLevel}, {})
					else
						addText('*'.repeat(headingLevel))
				} else {
					doMarkup('bold')
				}
			} else if (c == "/") {
				doMarkup('italic')
			} else if (c == "_") {
				doMarkup('underline')
			} else if (c == "~") {
				doMarkup('strikethrough')
				//============
				// >... quote
			} else if (startOfLine && eatChar(">")) {
				start_block('quote', {cite: null}, {})
				//==============
				// -... list/hr
			} else if (startOfLine && eatChar("-")) {
				//textBuffer = "" //hack:   /// what the heck why did i do this  *travelling to 2019 and sneaking up behind myself and pushing myself down the stairs*
				// it used to work since textbuffer got flushed at EOL...
				textBuffer = textBuffer.replace(/ +$/, "")
				//----------
				// --... hr
				if (eatChar("-")) {
					let count = 2
					while (eatChar("-"))
						count++
					//-------------
					// ---<EOL> hr
					if (c == "\n" || !c) { //this is kind of bad
						add_block('divider', null)
						//----------
						// ---... normal text
					} else {
						addText("-".repeat(count))
					}
					//------------
					// - ... list
				} else if (eatChar(" ")) {
					start_block('list', {}, {level: leadingSpaces})
					start_block('list_item', null, {level:leadingSpaces})
					//---------------
					// - normal char
				} else
					addText("-")
				//==========================
				// ] end link if inside one
			} else if (c == "]" && stack_top().inBrackets){ //this might break if it assumes .top() exists. needs more testing
				scan()
				if (stack_top().big) {
					if (eatChar("]"))
						endBlock()
					else
						addText("]")
				} else
					endBlock()
				//============
				// |... table
			} else if (c == "|") {
				let top = stack_top()
				// continuation
				if (top.type == 'table_cell') {
					scan()
					let row = top.row
					let table = top.row.table
					let eaten = eatChar("\n")
					//--------------
					// | | next row
					if (eaten && eatChar("|")) {
						// number of cells in first row
						// determines number of columns in table
						if (table.columns == null)
							table.columns = row.cells
						// end blocks
						endBlock() //cell
						if (top_is('table_row')) //always
							endBlock()
						// start row
						// calculate number of cells in row which will be
						// already filled due to previous row-spanning cells
						let cells = 0
						table.rowspans = table.rowspans.map((span)=>{
							cells++
							return span-1
						}).filter(span => span>0)
						row = start_block('table_row', null, {table:table, cells:cells})
						row.header = eatChar("*")
						// start cell
						startCell(row)
						//--------------------------
						// | next cell or table end
					} else {
						row.cells++
						textBuffer = textBuffer.replace(/ *$/,"") //strip trailing spaces (TODO: allow \<space>)
						// end of table
						// table ends when number of cells in current row = number of cells in first row
						// single-row tables are not easily possible ..
						// TODO: fix single row tables
						if (table.columns != null && row.cells > table.columns) {
							endBlock() //end cell
							if (top_is('table_row')) //always
								endBlock() //row
							if (top_is('table')) //always
								endBlock() //table
							if (eaten)
								addLineBreak()
						} else { // next cell
							endBlock() //cell
							startCell(row)
						}
					}
					// start of new table (must be at beginning of line)
				} else if (startOfLine) {
					scan()
					let table = start_block('table', null, {columns: null, rowspans: []})
					let row = start_block('table_row', null, {table: table, cells: 0})
					row.header = eatChar("*")
					startCell(row)
				} else {
					scan()
					addText("|")
				}
				//===========
				// `... code
			} else if (eatChar("`")) {
				//---------------
				// ``...
				if (eatChar("`")) {
					//----------------
					// ``` code block
					if (eatChar("`")) {
						// read lang name
						let start = i
						while (c && c!="\n" && c!="`")
							scan()
						//treat first line as language name, if it matches the pattern. otherwise it's code
						let language = code.substring(start, i)
						let eaten = false
						if (/^\s*\w*\s*$/.test(language)) {
							language = language.trim().toLowerCase()
							eaten = eatChar("\n")
							start = i
						}
						
						i = code.indexOf("```", i)
						let text = code.substring(start, i!=-1 ? i : code.length)
						add_block('code', {lang:language||'sb', text})
						skipNextLineBreak = eaten
						restore(i==-1 ? code.length : i+3)
						//------------
						// `` invalid
					} else {
						addText("``")
					}
					// --------------
					// ` inline code
				} else {
					let start = i
					let codeText = ""
					while (c) {
						if (c=="`") {
							if (code[i+1] != "`")
								break
							if (i == start+1 && codeText[0] == " ")
								codeText = codeText.substr(1)
							scan()
						}
						codeText += c
						scan()
					}
					add_block('icode', {text:codeText})
					scan()
				}
				//
				//================
				// link
			} else if (readLink()) {
				//
				//=============
				// normal char
			} else {
				addText(c)
				scan()
			}
		}
		// END
		endAll()
		return tree
		
		function endAll() {
			flushText()
			while (stack.length)
				endBlock()
		}
		
		// ###################################
		
		function readBracketedLink(embed) {
			if (eatChar("[")) {
				if (eatChar("[")) {
					// read url:
					let start = i
					let after = false
					let url = readUrl(true)
					if (eatChar("]")) {
						if (eatChar("]")) {
						} else if (eatChar("["))
							after = true
					}
					if (embed) {
						let [type, args] = urlType(url)
						if (after) {
							let altText = ""
							while (c) {
								if (c==']' && code[i+1]==']') { //messy
									scan()
									scan()
									break
								}
								eatChar("\\")
								altText += c
								scan()
							}
							args.alt = altText
						}
						add_block(type, args)
					} else {
						if (after)
							start_block('link', {url}, {big: true, inBrackets: true})
						else
							add_block('simple_link', {text:url, url})
					}
					return true
				} else {
					addText("[")
				}
			}
			return false
		}
		
		function readEnv() {
			if (!eatChar("{"))
				return false
			start_block(null, null, {})
			lineStart()
			
			let start = i
			if (eatChar("#")){
				let name = readTagName()
				let props = readProps()
				// todo: make this better lol
				let arg = props[""]
				if (name=='spoiler' && !stackContains("spoiler")) {
					let label = arg==true ? "spoiler" : arg
					start_block('spoiler', {label}, {})
				} else if (name=='ruby') {
					start_block('ruby', {text: String(arg)}, {})
				} else if (name=='align') {
					if (!(arg=='center'||arg=='right'||arg=='left'))
						arg = null
					start_block('align', {align: arg}, {})
				} else if (name=='anchor') {
					start_block('anchor', {name: String(arg)}, {})
				} else if (name=='bg') {
					// TODO: validate
					start_block('background_color', {color: String(arg)}, {})
				} else if (name=='sub') {
					start_block('subscript', null, {})
				} else if (name=='sup') {
					start_block('superscript', null, {})
				} else {
					add_block('invalid', {text:code.substring(start, i), reason:"invalid tag"})
				}
				/*if (displayBlock({type:name}))
				  skipNextLineBreak = true //what does this even do?*/
			}
			lineStart()
			return true
		}
		
		// read table cell properties and start cell block, and eat whitespace
		// assumed to be called when pointing to char after |
		function startCell(row) {
			let props = {}
			if (eatChar("#"))
				props = readProps()
			
			if (props.rs)
				row.table.rowspans.push(props.rs-1)
			if (props.cs)
				row.cells += props.cs-1
			
			let args = convert_cell_args(props, row.header)
			
			start_block('table_cell', args, {row: row})
			while (eatChar(" ")){
			}
		}
		
		// split string on first occurance
		function split1(string, sep) {
			let n = string.indexOf(sep)
			if (n == -1)
				return [string, null]
			else
				return [string.substr(0,n), string.substr(n+sep.length)]
		}
		
		function readTagName() {
			let start = i
			while (c>="a" && c<="z")
				scan()
			if (i > start)
				return code.substring(start, i)
		}
		
		// read properties key=value,key=value... ended by a space or \n or } or {
		// =value is optional and defaults to `true`
		function readProps() {
			let start = i
			let end = code.indexOf(" ", i)
			if (end < 0)
				end = code.length
			let end2 = code.indexOf("\n", i)
			if (end2 >= 0 && end2 < end)
				end = end2
			end2 = code.indexOf("}", i)
			if (end2 >= 0 && end2 < end)
				end = end2
			end2 = code.indexOf("{", i)
			if (end2 >= 0 && end2 < end)
				end = end2
			
			restore(end)
			eatChar(" ")
			
			let propst = code.substring(start, end)
			let props = {}
			for (let x of propst.split(",")) {
				let pair = split1(x, "=")
				if (pair[1] == null)
					pair[1] = true
				props[pair[0]] = pair[1]
			}
			return props
		}
		
		function readLink() {
			let embed = eatChar("!")
			if (readBracketedLink(embed) || readPlainLink(embed))
				return true
			if (embed) {
				addText("!")
				return true
				//lesson: if anything is eaten, you must return true if it's in the top level if switch block
			}
		}
		
		function readPlainLink(embed) {
			if (!isUrlStart()) return
			
			let url = readUrl()
			let after = eatChar("[")
			
			if (embed) {
				let [type, args] = urlType(url)
				if (after) {
					let altText = ""
					while (c && c!=']' && c!="\n") {
						eatChar("\\")
						altText += c
						scan()
					}
					scan()
					args.alt = altText
				}
				add_block(type, args)
			} else {
				if (after)
					start_block('link', {url}, {inBrackets: true})
				else
					add_block('simple_link', {text:url, url:url})
			}
			return true
		}
		
		// closeAll(true) - called at end of document
		// closeAll(false) - called at end of {} block
		function closeAll(force) {
			while (stack.length) {
				let top = stack_top()
				if (top.type == 'ROOT')
					break
				if (!force && top.type == null) {
					endBlock()
					break
				}
				endBlock()
			}
		}
		
		// called at the end of a line (unescaped newline)
		function endLine() {
			while (1) {
				let top = stack_top()
				if (top.type == 'heading' || top.type == 'quote') {
					endBlock()
				} else if (top.type == 'list_item') {
					if (top.type == 'list_item')
						endBlock()
					let indent = 0
					while (eatChar(" "))
						indent++
					// OPTION 1:
					// no next item; end list
					if (c != "-") {
						while (top_is('list')) //should ALWAYS happen at least once
							endBlock()
						addText(" ".repeat(indent))
					} else {
						scan()
						while (eatChar(" ")) {}
						// OPTION 2:
						// next item has same indent level; add item to list
						if (indent == top.level) {
							start_block('list_item', null, {level: indent})
							// OPTION 3:
							// next item has larger indent; start nested list
						} else if (indent > top.level) {
							start_block('list', {}, {level: indent})
							// then made the first item of the new list
							start_block('list_item', null, {level: indent})
							// OPTION 4:
							// next item has less indent; try to exist 1 or more layers of nested lists
							// if this fails, fall back to just creating a new item in the current list
						} else {
							// TODO: currently this will just fail completely
							while (1) {
								top = stack_top()
								if (top && top.type == 'list') {
									if (top.level <= indent)
										break
									endBlock()
								} else {
									// no suitable list was found :(
									// so just create a new one
									start_block('list', {}, {level: indent})
									break
								}
							}
							start_block('list_item', null, {level: indent})
						}
						break //really?
					}
				} else {
					addLineBreak()
					break
				}
			}
		}
		
		// audio, video, image, youtube
		function urlType(url) {
			if (/(\.mp3(?!\w)|\.ogg(?!\w)|\.wav(?!\w)|#audio$)/i.test(url))
				return ["audio", {url}]
			if (/(\.mp4(?!\w)|\.mkv(?!\w)|\.mov(?!\w)|#video$)/i.test(url))
				return ["video", {url}]
			let m = /^https?:[/][/](?:www[.])?(?:youtube.com[/]watch[?]v=|youtu[.]be[/])([\w-]{11,})(?:[&?](.*))?$/.exec(url)
			if (m)
				return ["youtube", {url, id:m[1]}]
			let size = /^([^#]*)#(\d+)x(\d+)$/.exec(url)
			if (size)
				return ["image", {url:size[1], width:+size[2], height:+size[3]}]
			return ["image", {url}]
		}
		
		// common code for all text styling tags (bold etc.)
		function doMarkup(type) {
			let symbol = c
			scan()
			if (canStartMarkup(type))
				start_block(type, null, {})
			else if (canEndMarkup(type))
				endBlock()
			else
				addText(symbol)
		}
		
		function canStartMarkup(type) {
			return (
				(!code[i-2] || char_in(code[i-2], " \t\n({'\"")) && //prev char is one of these (or start of text)
					(c && !char_in(c, " \t\n,'\"")) && //next char is not one of these
					!stackContains(type)
			)
		}
		function canEndMarkup(type) {
			return (
				top_is(type) && //there is an item to close
					!char_in(code[i-2], " \t\n,'\"") && //prev char is not one of these
					(!c || char_in(c, " \t\n-.,:!?')}\"")) //next char is one of these (or end of text)
			)
		}
		function char_in(chr, list) {
			return chr && list.indexOf(chr) != -1
		}
		
	}
	
	// start_block
	const block_translate = MAP({
		// things without arguments
		b: 'bold',
		i: 'italic',
		u: 'underline',
		s: 'strikethrough',
		sup: 'superscript',
		sub: 'subscript',
		table: 'table',
		tr: 'table_row',
		item: 'list_item',
		// with args
		td(args) {
			return ['table_cell', convert_cell_args(args)]
		},
		th(args) {
			return ['table_cell', convert_cell_args(args, true)]
		},
		align(args) {
			let align = args['']
			if (align!='left' && align!='right' && align!='center')
				align = null
			return ['align', {align}]
		},
		list(args) {
			return ['list', {style: args['']}]
		},
		spoiler(args) {
			return ['spoiler', {label: args['']}]
		},
		ruby(args) {
			return ['ruby', {text: args['']}]
		},
		quote(args) {
			return ['quote', {cite: args['']}]
		},
		anchor(args) {
			return ['anchor', {name: args['']}]
		},
		h1(args) {
			return ['heading', {level:1}]
		},
		h2(args) {
			return ['heading', {level:2}]
		},
		h3(args) {
			return ['heading', {level:3}]
		},
		url(args) {
			return ['link', {url: args['']}]
		},
		code: 2,
		youtube: 2,
		audio: 2,
		video: 2,
		img: 2,
	})
	// add_block
	const block_translate_2 = MAP({
		code(args, contents) {
			let inline = args[""] == 'inline'
			if (inline)
				return ['icode', {text:contents}]
			else {
				if (contents[0]=="\n")
					contents = contents.substr(1)
				return ['code', {text:contents, lang:args.lang||'sb'}]
			}
		},
		url(args, contents) {
			return ['simple_link', {text:contents, url: contents}]
		},
		youtube(args, contents) {
			return ['youtube', {url: args['']}] // TODO: set id here
		},
		audio(args, contents) {
			return ['audio', {url: args['']}]
		},
		video(args, contents) {
			return ['audio', {url: args['']}]
		},
		img(args, contents) {
			return ['audio', {url: args['']}]
		},	
	})
	
	this.langs['bbcode'] = function(codeInput) {
		init(codeInput)
		curr.lang = 'bbcode'
		if (!codeInput)
			return tree
		
		let point = 0
		
		while (c) {
			//===========
			// [... tag?
			if (eatChar("[")) {
				point = i-1
				// [/... end tag?
				if (eatChar("/")) {
					let name = readTagName()
					// invalid end tag
					if (!eatChar("]") || !name) {
						cancel()
						// valid end tag
					} else {
						// end last item in lists (mostly unnecessary now with greedy closing)
						if (name == "list" && stack_top().type == "list_item")
							endBlock(point)
						if (greedyCloseTag(name)) {
							// eat whitespace between table cells
							if (name == 'td' || name == 'th' || name == 'tr')
								while(eatChar(' ')||eatChar('\n')){
								}
						} else {
							// ignore invalid block
							//addBlock('invalid', code.substring(point, i), "unexpected closing tag")
						}
					}
					// [... start tag?
				} else {
					let name = readTagName()
					if (!name || !block_translate[name]) {
						// special case [*] list item
						if (eatChar("*") && eatChar("]")) {
							if (stack_top().type == "list_item")
								endBlock(point)
							let top = stack_top()
							if (top.type == "list")
								start_block('list_item', null, {bbcode:'item'})
							else
								cancel()
						} else
							cancel()
					} else {
						// [tag=...
						let arg = true, args = {}
						if (eatChar("=")) {
							let start=i
							if (eatChar('"')) {
								start++
								while (c && c!='"')
									scan()
								if (c == '"') {
									scan()
									arg = code.substring(start, i-1)
								}
							} else {
								while (c && c!="]" && c!=" ")
									scan()
								if (c == "]" || c == " ")
									arg = code.substring(start, i)
							}
						}
						if (eatChar(" ")) {
							args = readArgList() || {}
						}
						if (arg !== true)
							args[""] = arg
						if (eatChar("]")) {
							// simple tag
							if (block_translate_2[name] && !(name=="url" && arg!==true)) {
								let endTag = "[/"+name+"]"
								let end = code.indexOf(endTag, i)
								if (end < 0)
									cancel()
								else {
									let contents = code.substring(i, end)
									restore(end + endTag.length)
									
									let [t, a] = block_translate_2[name](args, contents)
									add_block(t, a)
								}
							} else if (name!="item" && block_translate[name] && !(name=='spoiler' && stackContains(name))) {
								if (name == 'tr' || name == 'table')
									while (eatChar(' ')||eatChar('\n')) {}
								
								let tx = block_translate[name]
								if (typeof tx == 'string')
									start_block(tx, null, {bbcode:name})
								else {
									let [t, a] = tx(args)
									start_block(t, a, {bbcode:name})
								}
							} else
								add_block('invalid', {text: code.substring(point, i), message:"invalid tag"})
						} else
							cancel()
					}
				}
			} else if (readPlainLink()) {
			} else if (eatChar('\n')) {
				addLineBreak()
			} else {
				addText(c)
				scan()
			}
		}
		endAll()
		return tree
		
		function cancel() {
			restore(point)
			addText(c)
			scan()
		}
		
		function greedyCloseTag(name) {
			for (let j=0; j<stack.length; j++)
				if (stack[j].bbcode == name) {
					while (stack_top().bbcode != name)//scary
						endBlock()
					endBlock()
					return true
				}
		}
		
		function readPlainLink() {
			if (isUrlStart()) {
				let url = readUrl()
				add_block('simple_link', {text:url, url:url})
				return true
			}
		}
		
		function readArgList() {
			let args = {}
			while (1) {
				// read key
				let start = i
				while (isTagChar(c))
					scan()
				let key = code.substring(start, i)
				// key=...
				if (eatChar("=")) {
					// key="...
					if (eatChar('"')) {
						start = i
						while (c && c!='"' && c!="\n")
							scan()
						if (eatChar('"'))
							args[key] = code.substring(start, i-2)
						else
							return null
						// key=...
					} else {
						start = i
						while (c && c!=" " && c!="]" && c!="\n")
							scan()
						if (c == "]") {
							args[key] = code.substring(start, i)
							return args
						} else if (eatChar(" ")) {
							args[key] = code.substring(start, i-1)
						} else
							return null
					}
					// key ...
				} else if (eatChar(" ")) {
					args[key] = true
					// key]...
				} else if (c == "]") {
					args[key] = true
					return args
					// key<other char> (error)
				} else
					return null
			}
		}
		
		function readTagName() {
			let start = i
			while (isTagChar(c))
				scan()
			return code.substring(start, i)
		}
		
		function isTagChar(c) {
			return c>="a"&&c<="z" || c>="A"&&c<="Z" || c>="0"&&c<="9"
		}
	}
	
	this.langs['plaintext'] = function(text) {
		let root = {type:'ROOT', content:[]}
		
		let linkRegex = /\b(?:https?:\/\/|sbs:)[-\w\$\.+!*'(),;/\?:@=&#%]*/g
		let result
		let last = 0
		while (result = linkRegex.exec(text)) {
			// text before link
			let before = text.substring(last, result.index)
			if (before)
				root.content.push(before)
			// generate link
			let url = result[0]
			root.content.push({type:'simple_link', args:{url:url, text:url}})
			last = result.index + result[0].length
		}
		// text after last link (or entire message if no links were found)
		let after = text.substr(last)
		if (after)
			root.content.push(after)
		
		return root
	}
	
	/**
		default markup language (plaintext)
		@instance
		@type {Parser_Function}
	*/
	this.default_lang = this.langs['plaintext']
}}

if ('object'==typeof module && module) module.exports = Markup_Legacy
