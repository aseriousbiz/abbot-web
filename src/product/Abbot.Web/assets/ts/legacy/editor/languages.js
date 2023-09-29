/**
 * @typedef CompilationError
 * @type object
 * @property {string} id - The error id of the compilation error.
 * @property {number} lineStart
 * @property {number} lineEnd
 * @property {number} spanStart
 * @property {number} spanEnd
 * @property {string} description
 * @property {string} errorMessage
 */

/**
 * 
 * @param error
 * @returns {CompilationError}
 */
function getNormalizedError(error) {
    return {
        id: error.errorId,
        lineStart: error.lineStart,
        lineEnd: error.lineEnd,
        spanStart: error.spanStart || 0,
        spanEnd: error.spanEnd || 0,
        description: error.description,
        errorMessage: ''
    };
}

/**
 * @typedef Language
 * @type {object}
 * @property {string} mode - The codemirror mode
 * @property parseError - method that parses a compilation error returns an error object.
 */

/**
 * @typedef LanguagesHash
 * @type {object}
 * @property {string} name - The language name
 * @property {Language} - The language object associated with the name.
 */

/** @type {LanguagesHash} **/
const languages = {
    Diff: {
        mode: 'text/x-diff'
    },
    CSharp: {
        mode: 'text/x-csharp',
        parseError: function(error) {
            const e = getNormalizedError(error);
            const reportedLineStart = e.lineStart + 1;
            e.errorMessage = `Line ${reportedLineStart}: [${error.errorId}] ${error.description}\n`;
            return e;
        }
    },
    Python: {
        mode: 'text/x-python',
        parseError: function(error) {
            const e = getNormalizedError(error);
            if (e.lineStart > 0) {
                const reportedLineStart = e.lineStart + 1;
                e.lineEnd = e.lineStart + 1; // Python does not report column information in errors.
                e.errorMessage = `[${e.id}] ${e.description} (Line ${reportedLineStart})\n`;
            } else {
                e.errorMessage = `[${e.id}] ${e.description}\n`;
            }
            return e; 
        }
    },
    JavaScript: {
        mode: 'text/javascript',
        parseError: function(error) {
            const e = getNormalizedError(error);
            const reportedLineStart = e.lineStart + 1; // Adding a line since we inject one in the editor.
            // eslint-disable-next-line no-self-assign
            e.lineStart = e.lineStart;
            e.lineEnd = e.lineStart + 1; // JavaScript does not report column data in errors.
            if (e.lineStart > 0) {
                e.errorMessage = `Line ${reportedLineStart}: [${e.id}] ${e.description}\n`;
            } else {
                e.errorMessage = `[${e.id}] ${e.description}\n`;
            }
            return e;
        }
    },
    Ink: {
        mode: 'text/x-ink',
        parseError: function (error) {
            const e = getNormalizedError(error);
            const reportedLineStart = e.lineStart + 1;
            e.errorMessage = `Line ${reportedLineStart}: [${error.errorId}] ${error.description}\n`;
            return e;
        }
    },

}



/**
 * Returns a language object for the given language. This is used to configure 
 * editor behavior.
 *
 * @param {string} language - The name of the code language.
 * @returns {Language} - The language object associated with the language name.
 * 
 * CREDIT: Adapted from https://stackoverflow.com/a/155812
 */
export default function getLanguage(language) {
    return languages[language];
}
