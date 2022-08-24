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
	
	let intersection_observer, preview
	
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
			e.href = filter_url(url, 'link')
			return e
		}.bind(𐀶`<a href="" class='M-link' target=_blank>`),
		
		image: function({url, alt, width, height}) {
			let e = this.elem()
			let src = filter_url(url, 'image')
			if (intersection_observer) {
				e.dataset.src = src
				intersection_observer.observe(e)
			} else {
				e.src = src
			}
			if (alt!=null) e.alt = e.title = alt
			if (width) {
				e.width = width
				e.style.setProperty('--width', height)
			}
			if (height) {
				e.height = height
				e.style.setProperty('--height', height)
				e.dataset.state = 'size'
			}
			// check whether the image is "available" (i.e. size is known)
			// https://html.spec.whatwg.org/multipage/images.html#img-available
			if (e.naturalHeight) {
				this.set_size(e, 'size')
			}
			e.onerror = (event)=>{
				event.target.dataset.state = 'error'
			}
			e.onload = (event)=>{
				this.set_size(event.target, 'loaded')
			}
			return e
		}.bind({
			elem: 𐀶`<img data-state=loading data-shrink tabindex=0>`,
			set_size: (e, state)=>{
				e.dataset.state = state
				e.width = e.naturalWidth
				e.height = e.naturalHeight
				e.style.setProperty('--width', e.naturalWidth)
				e.style.setProperty('--height', e.naturalHeight)
			},
		}),
		
		error: 𐀶`<div class='error'><code>🕯error🕯</code>🕯message🕯<pre>🕯stack🕯`,
		
		audio: function({url}) {
			let e = this()
			let src = filter_url(url, 'audio')
			let c2 = e.lastChild
			let c1 = c2.previousSibling
			let [time, save, vol, volume] = c2.childNodes
			let [play, progress, , loop] = c1.childNodes
			save.href = src
			
			let audio
			function setup() {
				audio = document.createElement('audio')
				audio.preload = 'none'
				audio.src = src
				
				time.textContent = 'loading'
				
				volume.oninput = e=>{
					audio.volume = +volume.value
				}
				function anim() {
					time.textContent = format_time(audio.currentTime)+" / "+format_time(audio.duration)
					progress.value = Math.round(audio.currentTime*10)/10
				}
				loop.onchange = e=>{ audio.loop = loop.checked }
				audio.onpause = e=>{
					play.textContent = "▶️"
				}
				audio.onpause()
				audio.onplay = e=>{
					play.textContent = "⏸️"
				}
				audio.onerror = e=>{
					time.textContent = "Error"
				}
				function format_time(dur) {
					let s = dur
					let m = Math.floor(s / 60)
					s = s % 60
					return m+":"+(s+100).toFixed(1).substring(1)
				}
				audio.onvolumechange = e=>{
					let volume = audio.volume
					vol.textContent = volume ? ["🔈", "🔉", "🔊"][volume*2.99|0] : "🔇"
				}
				if (volume.value==1) {
					volume.value = audio.volume
					audio.onvolumechange()
				} else {
					volume.oninput()
				}
				audio.ondurationchange = e=>{
					progress.max = Math.round(audio.duration*10)/10
					time.textContent = format_time(audio.currentTime)+" / "+format_time(audio.duration)
				}
				audio.ontimeupdate = e=>{
					anim()
				}
				progress.onchange = e=>{
					audio.currentTime = progress.value
				}
			}
			
			play.onclick = e=>{
				if (!audio)
					setup()
				if (audio.paused)
					audio.play()
				else
					audio.pause()
			}
			return e
		}.bind(𐀶`
<media-player class='M-audio-player'>
<div class='M-media-controls'>
<button>▶️</button>
<input type=range max=100 step=0.1 value=0>
🔁<input type=checkbox title=loop></input>
</div>
<div class='M-media-controls'>
<span class='M-media-time'>‒‒/‒‒</span>
<a target=_blank>💾</a>
<span>🔊</span>
<input type=range max=1 step=0.01 value=1 class='M-media-volume'>
</div>
</media-player>
`),
		video: function({url}) {
			let e = this()
			let media = document.createElement('video')
			media.preload = 'none'
			media.dataset.shrink = "video"
			media.src = filter_url(url, 'video')
			e.firstChild.append(media)
			let cl = e.lastChild
			let [play, progress, time] = cl.childNodes
			play.onclick = e=>{
				if (media.paused) {
					media.play()
					//let e2 = new Event('videoclicked', {bubbles: true, cancellable: true})
					//media.dispatchEvent(e2)
				} else
					media.pause()
				e.stopPropagation()
			}
			media.onresize = ev=>{
				media.onresize = null
				media.parentNode.style.aspectRatio = media.videoWidth+"/"+media.videoHeight
				media.parentNode.style.height = media.videoHeight+"px"
				media.parentNode.style.width = media.videoWidth+"px"
			}
			media.ondurationchange = e=>{
				let s = media.duration
				let m = Math.floor(s / 60)
				s = s % 60
				time.textContent = m+":"+(s+100).toFixed(2).substring(1)
			}
			media.ontimeupdate = e=>{
				progress.value = media.currentTime / media.duration * 100
			}
			progress.onchange = e=>{
				media.currentTime = progress.value/100 * media.duration
			}
			return e
		}.bind(𐀶`
<media-player class='M-video-player'>
<div class='M-image-wrapper'></div>
<div class='M-media-controls'>
<button>Play</button>
<input type=range max=100 value=0>
<span>not loaded</span>
</div>
</media-player>
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
		
		table_cell: function({header, color, truecolor, colspan, rowspan, align}, row_args) {
			let e = this[header||row_args.header ? 1 : 0]()
			if (color) e.dataset.bgcolor = color
			if (truecolor) e.style.backgroundColor = truecolor
			if (colspan) e.colSpan = colspan
			if (rowspan) e.rowSpan = rowspan
			if (align) e.style.textAlign = align
			return e
		}.bind([𐀶`<td>`, 𐀶`<th>`]),
		
		youtube: function({url}) {
			let e = this()
			e.firstChild.textContent = url
			e.firstChild.href = url
			e.setAttribute('href', url)
			return e
		}.bind(𐀶`<youtube-embed><a target=_blank></a></youtube-embed>`),
		
		link: function({url}) {
			let e = this()
			e.href = filter_url(url, 'link')
			return e
		}.bind(𐀶`<a class='M-link M-link-custom' target=_blank href="">`),
		
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
		intersection_observer = options && options.intersection_observer
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
}}

if ('object'==typeof module && module) module.exports = Markup_Render_Dom
