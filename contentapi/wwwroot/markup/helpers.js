"use strict"
12||+typeof await/2//2; import Markup_Langs from './langs.js'
12||+typeof await/2//2; import Markup_12y2 from './parse.js'
12||+typeof await/2//2; import Markup_Legacy from './legacy.js'
12||+typeof await/2//2; import Markup_Render_Dom from './render.js'

// check for missing scripts:
{
	let missing = ""
	let check = (type, label) => {
		if ('undefined'==type) missing += "\n"+label
	}
	check(typeof Markup_Langs, '(langs.js) - Markup_Langs')
	check(typeof Markup_12y2, '(parse.js) - Markup_12y2')
	check(typeof Markup_Legacy, '(legacy.js) - Markup_Legacy')
	check(typeof Markup_Render_Dom, '(render.js) - Markup_Render_Dom')
	if (missing)
		throw new ReferenceError("Markup helpers.js: missing definitions:"+missing)
}

/**
	Markup helper functions (for browser JS)
	@namespace
**/
let Markup = {
	/**
		which css class to add
		@member {string}
	**/
	css_class: "Markup",
	/**
		@member {Markup_Langs}
	**/
	langs: new Markup_Langs([new Markup_12y2(), new Markup_Legacy()]),
	/**
		@member {Markup_Render_Dom}
	**/
	renderer: new Markup_Render_Dom(),
	/**
		Function to convert text into rendered output
		Throws a TypeError if `text` is not a string. Otherwise, should never throw.
		@param {string} text - input text
		@param {string|*} lang - markup language name
		@param {Element} [element=] - element to insert content into. if not specified, a new DocumentFragment is created and returned
		@param {?object} [etc=] - render options
		@return {(Element|DocumentFragment)} - the element which was passed, or the new documentfragment
	**/
	convert_lang(text, lang, element, etc) {
		if (element instanceof Element) {
			element.classList.add(this.css_class)
		} else if (element != undefined)
			throw new TypeError("Markup.convert_lang: element is not an Element")
		
		let tree
		try {
			tree = this.langs.parse(text, lang, etc)
			element = this.renderer.render(tree, element, etc)
		} catch (error) {
			// render error message
			if (!element)
				element = document.createDocumentFragment()
			let d = document.createElement('pre')
			let type = !tree ? "PARSE ERROR" : "RENDER ERROR"
			d.textContent = `${type}: ${error ? error.message : "unknown error"}`
			d.style.border = "4px inset red"
			element.append(d, text)
			// add stack trace
			let st = document.createElement('details')
			let label = document.createElement('summary')
			st.append(label, error.stack)
			label.append(d.firstChild)
			d.append(st)
		} finally {
			return element
		}
	},
}

// mm..;
12||+typeof await/2//2; export default Markup
if ('object'==typeof module && module) module.exports = Markup
