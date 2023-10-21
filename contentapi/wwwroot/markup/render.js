"use strict"
12||+typeof await/2//2; export default
/**
	DOM node renderer (for use in browsers)
	factory class
**/
class Markup_Render_Dom { constructor() {
	// This tag-function parses an HTML string, and returns a function
	//  which creates a copy of that HTML DOM tree when called.
	// ex: let create = 𐀶`<div></div>`
	//  - create() acts like document.createElement('div')
	let temp = document.createElement('template')
	function 𐀶([html]) {
		temp.innerHTML = html.replace(/\s*?\n\s*/g, "")
		return document.importNode.bind(document, temp.content.firstChild, true)
	}
	
	// todo: this needs to be more powerful. i.e. returning entire elements in some cases etc.  gosh idk.. need to handle like, sbs emotes ? how uh nno that should be the parser's job.. oh and also this should, like,
	// for embeds, need separate handlers for normal urls and embeds and
	let URL_SCHEME = {
		__proto__: null,
		"sbs:": (url, thing)=> "#"+url.pathname+url.search+url.hash,
		"https:": (url, thing)=> url.href,
		"http:": (url, thing)=> url.href,
		"data:": (url, thing)=> url.href,
		DEFAULT: (url, thing)=> "about:blank#"+url.href,
		// these take a url string instead of URL
		RELATIVE: (href, thing)=> href.replace(/^[/]{0,2}/, "https://"),
		ERROR: (href, thing)=> "about:blank#"+href,
	}
	
	function filter_url(url, thing) {
		try {
			let u = new URL(url, "no-scheme:/")
			if ('no-scheme:'==u.protocol)
				return URL_SCHEME.RELATIVE(url, thing)
			else
				return (URL_SCHEME[u.protocol] || URL_SCHEME.DEFAULT)(u, thing)
		} catch (e) {
			return URL_SCHEME.ERROR(url, thing)
		}
	}
	
	let preview
	
	let CREATE = {
		__proto__: null,
		
		newline: 𐀶`<br>`,
		
		divider: 𐀶`<hr>`,
		
		code: function({text, lang}) { // <tt>?
			let e = this()
			e.textContent = text
			return e
		}.bind(𐀶`<pre>`),
		// .bind(value) makes that value accessible as `this` inside the function, when it's called. (note that the value is only evaluated once)
		// I'm just using this as a simple trick to store the html templates with their init functions, but there's no special reason to do it this way
		
		icode: function({text}) {
			let e = this()
			e.textContent = text.replace(/ /g, " ") // non breaking space..
			return e
		}.bind(𐀶`<code>`),
		
		simple_link: function({url, text}) {
			let e = this()
			if (text==null) {
				e.textContent = url
			} else {
				e.textContent = text
				e.className += ' M-link-custom'
			}
			if (!url.startsWith("#")) {
				url = filter_url(url, 'link')
				e.target = '_blank'
			} else
				e.target = '_self' //hack
			e.href = url
			return e
		}.bind(𐀶`<a href="" class='M-link'>`),
		
		image: function({url, alt, width, height}) {
			let src = filter_url(url, 'image')
			let e = document.createElement('img')
			e.classList.add('M-image')
			e.dataset.shrink = ""
			if (alt!=null)
				e.alt = e.title = alt
			e.tabIndex = 0
			const set_size = (state, width=e.naturalWidth, height=e.naturalHeight)=>{
				e.width = width
				e.height = height
				e.style.setProperty('--width', width)
				e.style.setProperty('--height', height)
				e.dataset.state = state
			}
			if (height)
				set_size('size', width, height)
			e.src = src
			// check whether the image is "available" (i.e. size is known) by looking at naturalHeight
			// https://html.spec.whatwg.org/multipage/images.html#img-available
			// this will happen here if the image is VERY cached, i guess
			if (e.naturalHeight)
				set_size('loaded')
			else // otherwise wait for load
				e.decode().then(ok=>{
					set_size('loaded')
				}, no=>{
					e.dataset.state = 'error'
				})
			return e
		},
		
		error: 𐀶`<div class='error'><code>🕯error🕯</code>🕯message🕯<pre>🕯stack🕯`,
		
		audio: function({url}) {
			url = filter_url(url, 'audio')
			let e = this()
			e.dataset.src = url
			e.onclick = ev=>{
				ev.preventDefault()
				let e = ev.currentTarget
				let audio = document.createElement('audio')
				audio.controls = true
				audio.autoplay = true
				audio.src = e.dataset.src
				e.replaceChildren(audio)
				e.onclick = null
			}
			let link = e.firstChild
			link.href = url
			link.title = url
			link.lastChild.textContent = url.replace(/.*[/]/, "…/")
			return e
		}.bind(𐀶`<y12-audio><a>🎵️<span></span></a></y12-audio>`),
		
		video: function({url}) {
			let e = this()
			let media = document.createElement('video')
			media.setAttribute('tabindex', 0)
			media.preload = 'none'
			media.dataset.shrink = "video"
			media.src = filter_url(url, 'video')
			e.firstChild.append(media)
			
			let cl = e.lastChild
			let [play, progress, time] = cl.childNodes
			play.onclick = e=>{
				if (media.paused)
					media.play()
				else
					media.pause()
				e.stopPropagation()
			}
			media.onpause = e=>{
				play.textContent = "▶️"
			}
			media.onplay = e=>{
				play.textContent = "⏸️"
			}
			media.onresize = ev=>{
				media.onresize = null
				media.parentNode.style.aspectRatio = media.videoWidth+"/"+media.videoHeight
				media.parentNode.style.height = media.videoHeight+"px"
				media.parentNode.style.width = media.videoWidth+"px"
			}
			media.onerror = ev=>{
				time.textContent = 'Error'
			}
			media.ondurationchange = e=>{
				let s = media.duration
				progress.disabled = false
				progress.max = s
				let m = Math.floor(s / 60)
				s = s % 60
				time.textContent = m+":"+(s+100).toFixed(2).substring(1)
			}
			media.ontimeupdate = e=>{
				progress.value = media.currentTime
			}
			progress.onchange = e=>{
				media.currentTime = progress.value
			}
			return e
		}.bind(𐀶`
<y12-video>
	<figure class='M-image-wrapper'></figure>
	<div class='M-media-controls'>
		<button>▶️</button>
		<input type=range min=0 max=1 step=any value=0 disabled>
		<span>not loaded</span>
	</div>
</y12-video>
`),
		
		italic: 𐀶`<i>`,
		
		bold: 𐀶`<b>`,
		
		strikethrough: 𐀶`<s>`,
		
		underline: 𐀶`<u>`,
		
		heading: function({level, id}) {
			let e = document.createElement("h"+(level- -1))
			if (id) {
				let e2 = this()
				e2.name = id
				e2.appendChild(e)
			}
			return e
		}.bind(𐀶`<a name="" class=M-anchor></a>`),
		
		// what if instead of the \a tag, we just supported
		// an [id=...] attribute on every tag? just need to set id, so...
		// well except <a name=...> is safer than id...
		anchor: function({id}) {
			let e = this()
			if (id)
				e.name = id
			return e
		}.bind(𐀶`<a name="" class=M-anchor></a>`),
		
		quote: function({cite}) {
			if (cite==null)
				return this[0]()
			let e = this[1]()
			e.firstChild.textContent = cite
			return e.lastChild
		}.bind([
			𐀶`<blockquote class='M-quote'>`,
			𐀶`<blockquote class='M-quote'><cite class='M-quote-label'></cite>:<div class='M-quote-inner'></div></blockquote>`, // should we have -outer class?
		]),
		
		table: function() {
			let e = this()
			return e.firstChild.firstChild
		}.bind(𐀶`<div class='M-table-outer'><table><tbody>`),
		
		table_row: 𐀶`<tr>`,
		
		table_cell: function({header, color, truecolor, colspan, rowspan, align, div}, row_args) {
			let e = this[header||row_args.header ? 1 : 0]()
			if (color) e.dataset.bgcolor = color
			if (truecolor) e.style.backgroundColor = truecolor
			if (colspan) e.colSpan = colspan
			if (rowspan) e.rowSpan = rowspan
			if (align) e.style.textAlign = align
			// todo: better way of representing this?
			if (div)
				e.classList.add('M-wall-right')
			if (row_args.divider)
				e.classList.add('M-wall-top')
			return e
		}.bind([𐀶`<td>`, 𐀶`<th>`]),
		
		youtube: function({url}) {
			let e = this()
			e.firstChild.textContent = url
			e.firstChild.href = url
			e.dataset.href = url
			return e
		}.bind(𐀶`<youtube-embed><a target=_blank></a></youtube-embed>`),
		
		link: function({url}) {
			let e = this()
			if (!url.startsWith("#")) {
				url = filter_url(url, 'link')
				e.target = '_blank'
			} else
				e.target = '_self'
			e.href = url
			return e
		}.bind(𐀶`<a class='M-link M-link-custom' href="">`),
		
		list: function({style}) {
			if (style==null)
				return this[0]()
			let e = this[1]()
			//e.style.listStyleType = style // this was only supported by old bbcode so i can probably secretly remove it.
			return e
		}.bind([𐀶`<ul>`, 𐀶`<ol>`]),
		
		/* todo: list bullets suck, because you can't select/copy them
we should create our own fake bullet elements instead.*/
		list_item: 𐀶`<li>`,
		
		align: function({align}) {
			let e = this()
			e.style.textAlign = align
			return e
		}.bind(𐀶`<div>`),
		
		subscript: 𐀶`<sub>`,
		
		superscript: 𐀶`<sup>`,
		
		/*anchor: function({name}) {
			let e = this()
			e.id = "Markup-anchor-"+name
			return e
		}.bind(𐀶`<span id="" class='M-anchor'>`),*/
		
		ruby: function({text}) {
			let e = this()
			e.lastChild.textContent = text
			return e.firstChild
		}.bind(𐀶`<ruby><span></span><rt>`), // I don't think we need <rp> since we're rendering for modern browsers...
		
		spoiler: function({label}) {
			let e = this()
			e.firstChild.textContent = label//.replace(/_/g, " ")
			//todo: [12y1] maybe replace all underscores in args with spaces, during parsing?
			return e.lastChild
		}.bind(𐀶`
<details class='M-spoiler'>
	<summary class='M-spoiler-label'></summary>
	<div class='M-spoiler-inner'></div>
</details>`),
		
		background_color: function({color}) {
			let e = this()
			if (color)
				e.dataset.bgcolor = color
			return e
		}.bind(𐀶`<span class='M-background'>`),
		
		invalid: function({text, reason}) {
			let e = this()
			e.title = reason
			e.textContent = text
			return e
		}.bind(𐀶`<span class='M-invalid'>`),
		
		key: 𐀶`<kbd>`,
		
		preview: function(node) {
			let e = this()
			e.textContent = node.type
			return e
		}.bind(𐀶`<div class='M-preview'>`),
	}
	
	function fill_branch(branch, leaves) {
		for (let leaf of leaves) {
			if ('string'==typeof leaf) {
				branch.append(leaf)
			} else {
				let node
				if (preview && (leaf.type=='audio' || leaf.type=='video' || leaf.type=='youtube')) {
					node = CREATE.preview(leaf)
				} else {
					let creator = CREATE[leaf.type]
					if (!creator) {
						if ('object'==typeof leaf && leaf)
							throw new RangeError("unknown node .type: ‘"+leaf.type+"’")
						else
							throw new TypeError("unknown node type: "+typeof leaf)
					}
					node = creator(leaf.args)
				}
				if (leaf.content) {
					if ('table_row'===leaf.type) {
						for (let cell of leaf.content) {
							if ('table_cell'!==cell.type)
								continue
							let c = CREATE.table_cell(cell.args, leaf.args||{})
							if (cell.content)
								fill_branch(c, cell.content)
							node.append(c)
						}
					} else {
						fill_branch(node, leaf.content)
					}
				}
				branch.append(node.getRootNode()) // recursion order?
			}
		}
	}
	/**
		Render function (closure method)
		@param {Tree} ast - input ast
		@param {ParentNode} [node=document.createDocumentFragment()] - destination node
		@param {?object} options - render options
		@return {ParentNode} - node with rendered contents. same as `node` if passed, otherwise is a new DocumentFragment.
	**/
	this.render = function({args, content}, node=document.createDocumentFragment(), options) {
		preview = options && options.preview
		node.textContent = "" //mmnn
		fill_branch(node, content)
		return node
	}
	/**
		block rendering functions
		@member {Object<string,function>}
	**/
	this.create = CREATE
	/**
		URL processing functions
		@member {Object<string,function>}
	**/
	this.url_scheme = URL_SCHEME
	this.filter_url = filter_url
}}

if ('object'==typeof module && module) module.exports = Markup_Render_Dom
