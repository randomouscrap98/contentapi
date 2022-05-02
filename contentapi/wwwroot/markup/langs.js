/**
	ast
	@typedef {Object} Tree
	@property {string} type - Node Type
	@property {?Object} args - arguments
	@property {?Array<(Tree|string)>} contents - contents
*/

/**
	parser function
	@typedef {function} Parser_Function
	@param {string} text - text to parse
	@return {Tree} - syntax tree
*/

/**
	Markup_Langs may inherit from these classes
	@interface Langs_Mixin
*/
/**
	@instance
	@type {Object<string,Parser_Function>}
	@name langs
	@memberof Langs_Mixin
*/
/**
	@instance
	@type {?Parser_Function}
	@name default_lang
	@memberof Langs_Mixin
*/

/**
	markup langs container
*/
class Markup_Langs {
	/**
		@param {Array<Langs_Mixin>} inherit - parsers to include
	*/
	constructor(include) {
		this.langs = Object.create(null)
		this.default_lang = function(text) {
			return {type:'ROOT', content:[text]}
		}
		for (let m of include) 
			this.include(m)
	}
	include(m) {
		if (m.langs)
			Object.assign(this.langs, m.langs)
		if (m.default_lang)
			this.default_lang = m.default_lang
	}
	/**
		@param {(string|*)} lang - markup language name
		@return {Parser_Function} - parser
	*/
	get(lang) {
		if ('string'!=typeof lang)
			return this.default_lang
		return this.langs[lang] || this.default_lang
	}
	/**
		@param {string} text - text to parse
		@param {(string|*)} lang - markup language name
		@return {Tree} - ast
	*/
	parse(text, lang) {
		if ('string'!=typeof text)
			throw new TypeError("parse: text is not a string")
		return this.get(lang)(text)
	}
}

if ('object'==typeof module && module) module.exports = Markup_Langs
