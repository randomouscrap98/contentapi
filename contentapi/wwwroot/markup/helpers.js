// check for missing scripts:
{
	let missing = ""
	let check = (type, label) => {
		if (type=='undefined') missing += "\n"+label
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
*/
let Markup = {
	/**
		which css class to add
		@type {string}
	*/
	css_class: "Markup",
	/**
		@type {Markup_Langs}
	*/
	langs: new Markup_Langs([new Markup_12y2(), new Markup_Legacy()]),
	/**
		@type {Markup_Render_Dom}
	*/
	renderer: new Markup_Render_Dom(),
	/**
		function to convert text into rendered output
		note: throws a TypeError if `text` is not a string. otherwise should never throw.
		@param {string} text - input text
		@param {string|*} lang - markup language name
		@param {Element} [element=] - element to insert content into. if not specified, a new DocumentFragment is created and returned
		@param {Object} [options=] - unused currently
		@return {(Element|DocumentFragment)} - the element which was passed, or the new documentfragment
	*/
	convert_lang(text, lang, element, options) {
		if (element instanceof Element) {
			element.classList.add(this.css_class)
		} else if (element!=undefined)
			throw new TypeError("Markup.convert_lang: element is not an Element")
		
		let tree, err
		try {
			tree = this.langs.parse(text, lang)
			element = this.renderer.render(tree, element)
		} catch (error) {
			if (!element)
				element = document.createDocumentFragment()
			let d = document.createElement('pre')
			let type = !tree ? "PARSE ERROR" : "RENDER ERROR"
			d.textContent = `${type}: ${error ? error.message : "unknown error"}`
			d.style.border = "4px inset red"
			element.append(d, text)
		} finally {
			return element
		}
	},
}
