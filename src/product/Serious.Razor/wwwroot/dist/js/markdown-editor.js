/*
 * ATTENTION: An "eval-source-map" devtool has been used.
 * This devtool is neither made for production nor for readable output files.
 * It uses "eval()" calls to create a separate source file with attached SourceMaps in the browser devtools.
 * If you are trying to read the output file, select a different devtool (https://webpack.js.org/configuration/devtool/)
 * or disable the default devtool with "devtool: false".
 * If you are looking for production-ready output files, see mode: "production" (https://webpack.js.org/configuration/mode/).
 */
/******/ (() => { // webpackBootstrap
/******/ 	"use strict";
/******/ 	var __webpack_modules__ = ({

/***/ "./assets/markdown-editor/index.js":
/*!*****************************************!*\
  !*** ./assets/markdown-editor/index.js ***!
  \*****************************************/
/***/ ((__unused_webpack_module, __webpack_exports__, __webpack_require__) => {

eval("__webpack_require__.r(__webpack_exports__);\n/* harmony import */ var _index_scss__WEBPACK_IMPORTED_MODULE_0__ = __webpack_require__(/*! ./index.scss */ \"./assets/markdown-editor/index.scss\");\n/* harmony import */ var _js_markdown_editor__WEBPACK_IMPORTED_MODULE_1__ = __webpack_require__(/*! ./js/markdown-editor */ \"./assets/markdown-editor/js/markdown-editor.js\");\n\n\ndocument.addEventListener(\"DOMContentLoaded\", function () {\n  // Apply Markdown Text Area.\n  (0,_js_markdown_editor__WEBPACK_IMPORTED_MODULE_1__[\"default\"])(document);\n});\n\n// Support Turbo, if present.\ndocument.addEventListener(\"turbo:render\", function () {\n  // Apply Markdown Text Area.\n  (0,_js_markdown_editor__WEBPACK_IMPORTED_MODULE_1__[\"default\"])(document);\n});//# sourceURL=[module]\n//# sourceMappingURL=data:application/json;charset=utf-8;base64,eyJ2ZXJzaW9uIjozLCJmaWxlIjoiLi9hc3NldHMvbWFya2Rvd24tZWRpdG9yL2luZGV4LmpzLmpzIiwibWFwcGluZ3MiOiI7OztBQUFzQjtBQUNvQztBQUUxREMsUUFBUSxDQUFDQyxnQkFBZ0IsQ0FBQyxrQkFBa0IsRUFBRSxZQUFXO0VBQ3JEO0VBQ0FGLCtEQUFzQixDQUFDQyxRQUFRLENBQUM7QUFDcEMsQ0FBQyxDQUFDOztBQUVGO0FBQ0FBLFFBQVEsQ0FBQ0MsZ0JBQWdCLENBQUMsY0FBYyxFQUFFLFlBQVc7RUFDakQ7RUFDQUYsK0RBQXNCLENBQUNDLFFBQVEsQ0FBQztBQUNwQyxDQUFDLENBQUMiLCJzb3VyY2VzIjpbIndlYnBhY2s6Ly9zZXJpb3VzLXJhem9yLy4vYXNzZXRzL21hcmtkb3duLWVkaXRvci9pbmRleC5qcz9iMGQ1Il0sInNvdXJjZXNDb250ZW50IjpbImltcG9ydCAnLi9pbmRleC5zY3NzJztcbmltcG9ydCBzZXR1cE1hcmtkb3duVGV4dEFyZWFzIGZyb20gJy4vanMvbWFya2Rvd24tZWRpdG9yJztcblxuZG9jdW1lbnQuYWRkRXZlbnRMaXN0ZW5lcihcIkRPTUNvbnRlbnRMb2FkZWRcIiwgZnVuY3Rpb24oKSB7XG4gICAgLy8gQXBwbHkgTWFya2Rvd24gVGV4dCBBcmVhLlxuICAgIHNldHVwTWFya2Rvd25UZXh0QXJlYXMoZG9jdW1lbnQpO1xufSk7XG5cbi8vIFN1cHBvcnQgVHVyYm8sIGlmIHByZXNlbnQuXG5kb2N1bWVudC5hZGRFdmVudExpc3RlbmVyKFwidHVyYm86cmVuZGVyXCIsIGZ1bmN0aW9uKCkge1xuICAgIC8vIEFwcGx5IE1hcmtkb3duIFRleHQgQXJlYS5cbiAgICBzZXR1cE1hcmtkb3duVGV4dEFyZWFzKGRvY3VtZW50KTtcbn0pOyJdLCJuYW1lcyI6WyJzZXR1cE1hcmtkb3duVGV4dEFyZWFzIiwiZG9jdW1lbnQiLCJhZGRFdmVudExpc3RlbmVyIl0sInNvdXJjZVJvb3QiOiIifQ==\n//# sourceURL=webpack-internal:///./assets/markdown-editor/index.js\n");

/***/ }),

/***/ "./assets/markdown-editor/js/markdown-editor.js":
/*!******************************************************!*\
  !*** ./assets/markdown-editor/js/markdown-editor.js ***!
  \******************************************************/
/***/ ((__unused_webpack_module, __webpack_exports__, __webpack_require__) => {

eval("__webpack_require__.r(__webpack_exports__);\n/* harmony export */ __webpack_require__.d(__webpack_exports__, {\n/* harmony export */   \"default\": () => (/* binding */ setupMarkdownTextAreas)\n/* harmony export */ });\nfunction asyncGeneratorStep(gen, resolve, reject, _next, _throw, key, arg) { try { var info = gen[key](arg); var value = info.value; } catch (error) { reject(error); return; } if (info.done) { resolve(value); } else { Promise.resolve(value).then(_next, _throw); } }\nfunction _asyncToGenerator(fn) { return function () { var self = this, args = arguments; return new Promise(function (resolve, reject) { var gen = fn.apply(self, args); function _next(value) { asyncGeneratorStep(gen, resolve, reject, _next, _throw, \"next\", value); } function _throw(err) { asyncGeneratorStep(gen, resolve, reject, _next, _throw, \"throw\", err); } _next(undefined); }); }; }\nfunction setupMarkdownTextAreas(document) {\n  document.querySelectorAll('tab-container').forEach(tabContainer => {\n    if (tabContainer.dataset.markdownEditor) {\n      return;\n    }\n    tabContainer.dataset.markdownEditor = \"true\";\n    var preview = tabContainer.querySelector('.preview-content');\n    var emptyPreviewMessage = preview.innerHTML;\n    var previewUrl = tabContainer.dataset.previewUrl;\n    var textarea = tabContainer.querySelector('textarea');\n    tabContainer.addEventListener('tab-container-changed', /*#__PURE__*/function () {\n      var _ref = _asyncToGenerator(function* (evt) {\n        tabContainer.dataset.selectedTab = evt.detail.relatedTarget.dataset.tab;\n        var markdown = textarea.value;\n        if (tabContainer.dataset.selectedTab === 'preview') {\n          if (markdown.length > 0) {\n            var response = yield fetch(previewUrl, {\n              method: 'POST',\n              headers: {\n                'Accept': 'application/json',\n                'Content-Type': 'application/json'\n              },\n              body: JSON.stringify(markdown)\n            });\n            if (response.ok) {\n              preview.innerHTML = yield response.text();\n            } else {\n              preview.innerHTML = 'Sorry, there was a problem fetching the preview.';\n            }\n          } else {\n            preview.innerHTML = emptyPreviewMessage;\n          }\n        }\n      });\n      return function (_x) {\n        return _ref.apply(this, arguments);\n      };\n    }());\n  });\n}//# sourceURL=[module]\n//# sourceMappingURL=data:application/json;charset=utf-8;base64,eyJ2ZXJzaW9uIjozLCJmaWxlIjoiLi9hc3NldHMvbWFya2Rvd24tZWRpdG9yL2pzL21hcmtkb3duLWVkaXRvci5qcy5qcyIsIm1hcHBpbmdzIjoiOzs7Ozs7QUFBZSxTQUFTQSxzQkFBc0IsQ0FBQ0MsUUFBUSxFQUFFO0VBQ3JEQSxRQUFRLENBQUNDLGdCQUFnQixDQUFDLGVBQWUsQ0FBQyxDQUFDQyxPQUFPLENBQUNDLFlBQVksSUFBSTtJQUMvRCxJQUFJQSxZQUFZLENBQUNDLE9BQU8sQ0FBQ0MsY0FBYyxFQUFFO01BQ3JDO0lBQ0o7SUFDQUYsWUFBWSxDQUFDQyxPQUFPLENBQUNDLGNBQWMsR0FBRyxNQUFNO0lBQzVDLElBQU1DLE9BQU8sR0FBR0gsWUFBWSxDQUFDSSxhQUFhLENBQUMsa0JBQWtCLENBQUM7SUFDOUQsSUFBTUMsbUJBQW1CLEdBQUdGLE9BQU8sQ0FBQ0csU0FBUztJQUM3QyxJQUFNQyxVQUFVLEdBQUdQLFlBQVksQ0FBQ0MsT0FBTyxDQUFDTSxVQUFVO0lBQ2xELElBQU1DLFFBQVEsR0FBR1IsWUFBWSxDQUFDSSxhQUFhLENBQUMsVUFBVSxDQUFDO0lBRXZESixZQUFZLENBQUNTLGdCQUFnQixDQUFDLHVCQUF1QjtNQUFBLDZCQUFFLFdBQU1DLEdBQUcsRUFBSTtRQUNoRVYsWUFBWSxDQUFDQyxPQUFPLENBQUNVLFdBQVcsR0FBR0QsR0FBRyxDQUFDRSxNQUFNLENBQUNDLGFBQWEsQ0FBQ1osT0FBTyxDQUFDYSxHQUFHO1FBQ3ZFLElBQU1DLFFBQVEsR0FBR1AsUUFBUSxDQUFDUSxLQUFLO1FBQy9CLElBQUloQixZQUFZLENBQUNDLE9BQU8sQ0FBQ1UsV0FBVyxLQUFLLFNBQVMsRUFBRTtVQUNoRCxJQUFJSSxRQUFRLENBQUNFLE1BQU0sR0FBRyxDQUFDLEVBQUU7WUFDckIsSUFBTUMsUUFBUSxTQUFTQyxLQUFLLENBQUNaLFVBQVUsRUFBRTtjQUNyQ2EsTUFBTSxFQUFFLE1BQU07Y0FDZEMsT0FBTyxFQUFFO2dCQUNMLFFBQVEsRUFBRSxrQkFBa0I7Z0JBQzVCLGNBQWMsRUFBRTtjQUNwQixDQUFDO2NBQ0RDLElBQUksRUFBRUMsSUFBSSxDQUFDQyxTQUFTLENBQUNULFFBQVE7WUFDakMsQ0FBQyxDQUFDO1lBQ0YsSUFBSUcsUUFBUSxDQUFDTyxFQUFFLEVBQUU7Y0FDYnRCLE9BQU8sQ0FBQ0csU0FBUyxTQUFTWSxRQUFRLENBQUNRLElBQUksRUFBRTtZQUM3QyxDQUFDLE1BQ0k7Y0FDRHZCLE9BQU8sQ0FBQ0csU0FBUyxHQUFHLGtEQUFrRDtZQUMxRTtVQUVKLENBQUMsTUFBTTtZQUNISCxPQUFPLENBQUNHLFNBQVMsR0FBR0QsbUJBQW1CO1VBQzNDO1FBQ0o7TUFDSixDQUFDO01BQUE7UUFBQTtNQUFBO0lBQUEsSUFBQztFQUNOLENBQUMsQ0FBQztBQUNOIiwic291cmNlcyI6WyJ3ZWJwYWNrOi8vc2VyaW91cy1yYXpvci8uL2Fzc2V0cy9tYXJrZG93bi1lZGl0b3IvanMvbWFya2Rvd24tZWRpdG9yLmpzPzQ1NzIiXSwic291cmNlc0NvbnRlbnQiOlsiZXhwb3J0IGRlZmF1bHQgZnVuY3Rpb24gc2V0dXBNYXJrZG93blRleHRBcmVhcyhkb2N1bWVudCkge1xuICAgIGRvY3VtZW50LnF1ZXJ5U2VsZWN0b3JBbGwoJ3RhYi1jb250YWluZXInKS5mb3JFYWNoKHRhYkNvbnRhaW5lciA9PiB7XG4gICAgICAgIGlmICh0YWJDb250YWluZXIuZGF0YXNldC5tYXJrZG93bkVkaXRvcikge1xuICAgICAgICAgICAgcmV0dXJuO1xuICAgICAgICB9XG4gICAgICAgIHRhYkNvbnRhaW5lci5kYXRhc2V0Lm1hcmtkb3duRWRpdG9yID0gXCJ0cnVlXCI7XG4gICAgICAgIGNvbnN0IHByZXZpZXcgPSB0YWJDb250YWluZXIucXVlcnlTZWxlY3RvcignLnByZXZpZXctY29udGVudCcpO1xuICAgICAgICBjb25zdCBlbXB0eVByZXZpZXdNZXNzYWdlID0gcHJldmlldy5pbm5lckhUTUw7XG4gICAgICAgIGNvbnN0IHByZXZpZXdVcmwgPSB0YWJDb250YWluZXIuZGF0YXNldC5wcmV2aWV3VXJsO1xuICAgICAgICBjb25zdCB0ZXh0YXJlYSA9IHRhYkNvbnRhaW5lci5xdWVyeVNlbGVjdG9yKCd0ZXh0YXJlYScpO1xuXG4gICAgICAgIHRhYkNvbnRhaW5lci5hZGRFdmVudExpc3RlbmVyKCd0YWItY29udGFpbmVyLWNoYW5nZWQnLCBhc3luYyBldnQgPT4ge1xuICAgICAgICAgICAgdGFiQ29udGFpbmVyLmRhdGFzZXQuc2VsZWN0ZWRUYWIgPSBldnQuZGV0YWlsLnJlbGF0ZWRUYXJnZXQuZGF0YXNldC50YWI7XG4gICAgICAgICAgICBjb25zdCBtYXJrZG93biA9IHRleHRhcmVhLnZhbHVlO1xuICAgICAgICAgICAgaWYgKHRhYkNvbnRhaW5lci5kYXRhc2V0LnNlbGVjdGVkVGFiID09PSAncHJldmlldycpIHtcbiAgICAgICAgICAgICAgICBpZiAobWFya2Rvd24ubGVuZ3RoID4gMCkge1xuICAgICAgICAgICAgICAgICAgICBjb25zdCByZXNwb25zZSA9IGF3YWl0IGZldGNoKHByZXZpZXdVcmwsIHtcbiAgICAgICAgICAgICAgICAgICAgICAgIG1ldGhvZDogJ1BPU1QnLFxuICAgICAgICAgICAgICAgICAgICAgICAgaGVhZGVyczoge1xuICAgICAgICAgICAgICAgICAgICAgICAgICAgICdBY2NlcHQnOiAnYXBwbGljYXRpb24vanNvbicsXG4gICAgICAgICAgICAgICAgICAgICAgICAgICAgJ0NvbnRlbnQtVHlwZSc6ICdhcHBsaWNhdGlvbi9qc29uJ1xuICAgICAgICAgICAgICAgICAgICAgICAgfSxcbiAgICAgICAgICAgICAgICAgICAgICAgIGJvZHk6IEpTT04uc3RyaW5naWZ5KG1hcmtkb3duKVxuICAgICAgICAgICAgICAgICAgICB9KTtcbiAgICAgICAgICAgICAgICAgICAgaWYgKHJlc3BvbnNlLm9rKSB7XG4gICAgICAgICAgICAgICAgICAgICAgICBwcmV2aWV3LmlubmVySFRNTCA9IGF3YWl0IHJlc3BvbnNlLnRleHQoKTtcbiAgICAgICAgICAgICAgICAgICAgfVxuICAgICAgICAgICAgICAgICAgICBlbHNlIHtcbiAgICAgICAgICAgICAgICAgICAgICAgIHByZXZpZXcuaW5uZXJIVE1MID0gJ1NvcnJ5LCB0aGVyZSB3YXMgYSBwcm9ibGVtIGZldGNoaW5nIHRoZSBwcmV2aWV3LidcbiAgICAgICAgICAgICAgICAgICAgfVxuXG4gICAgICAgICAgICAgICAgfSBlbHNlIHtcbiAgICAgICAgICAgICAgICAgICAgcHJldmlldy5pbm5lckhUTUwgPSBlbXB0eVByZXZpZXdNZXNzYWdlXG4gICAgICAgICAgICAgICAgfVxuICAgICAgICAgICAgfVxuICAgICAgICB9KVxuICAgIH0pO1xufVxuIl0sIm5hbWVzIjpbInNldHVwTWFya2Rvd25UZXh0QXJlYXMiLCJkb2N1bWVudCIsInF1ZXJ5U2VsZWN0b3JBbGwiLCJmb3JFYWNoIiwidGFiQ29udGFpbmVyIiwiZGF0YXNldCIsIm1hcmtkb3duRWRpdG9yIiwicHJldmlldyIsInF1ZXJ5U2VsZWN0b3IiLCJlbXB0eVByZXZpZXdNZXNzYWdlIiwiaW5uZXJIVE1MIiwicHJldmlld1VybCIsInRleHRhcmVhIiwiYWRkRXZlbnRMaXN0ZW5lciIsImV2dCIsInNlbGVjdGVkVGFiIiwiZGV0YWlsIiwicmVsYXRlZFRhcmdldCIsInRhYiIsIm1hcmtkb3duIiwidmFsdWUiLCJsZW5ndGgiLCJyZXNwb25zZSIsImZldGNoIiwibWV0aG9kIiwiaGVhZGVycyIsImJvZHkiLCJKU09OIiwic3RyaW5naWZ5Iiwib2siLCJ0ZXh0Il0sInNvdXJjZVJvb3QiOiIifQ==\n//# sourceURL=webpack-internal:///./assets/markdown-editor/js/markdown-editor.js\n");

/***/ }),

/***/ "./assets/markdown-editor/index.scss":
/*!*******************************************!*\
  !*** ./assets/markdown-editor/index.scss ***!
  \*******************************************/
/***/ ((__unused_webpack_module, __webpack_exports__, __webpack_require__) => {

eval("__webpack_require__.r(__webpack_exports__);\n// extracted by mini-css-extract-plugin\n//# sourceURL=[module]\n//# sourceMappingURL=data:application/json;charset=utf-8;base64,eyJ2ZXJzaW9uIjozLCJmaWxlIjoiLi9hc3NldHMvbWFya2Rvd24tZWRpdG9yL2luZGV4LnNjc3MuanMiLCJtYXBwaW5ncyI6IjtBQUFBIiwic291cmNlcyI6WyJ3ZWJwYWNrOi8vc2VyaW91cy1yYXpvci8uL2Fzc2V0cy9tYXJrZG93bi1lZGl0b3IvaW5kZXguc2Nzcz9iZWIzIl0sInNvdXJjZXNDb250ZW50IjpbIi8vIGV4dHJhY3RlZCBieSBtaW5pLWNzcy1leHRyYWN0LXBsdWdpblxuZXhwb3J0IHt9OyJdLCJuYW1lcyI6W10sInNvdXJjZVJvb3QiOiIifQ==\n//# sourceURL=webpack-internal:///./assets/markdown-editor/index.scss\n");

/***/ })

/******/ 	});
/************************************************************************/
/******/ 	// The module cache
/******/ 	var __webpack_module_cache__ = {};
/******/ 	
/******/ 	// The require function
/******/ 	function __webpack_require__(moduleId) {
/******/ 		// Check if module is in cache
/******/ 		var cachedModule = __webpack_module_cache__[moduleId];
/******/ 		if (cachedModule !== undefined) {
/******/ 			return cachedModule.exports;
/******/ 		}
/******/ 		// Create a new module (and put it into the cache)
/******/ 		var module = __webpack_module_cache__[moduleId] = {
/******/ 			// no module.id needed
/******/ 			// no module.loaded needed
/******/ 			exports: {}
/******/ 		};
/******/ 	
/******/ 		// Execute the module function
/******/ 		__webpack_modules__[moduleId](module, module.exports, __webpack_require__);
/******/ 	
/******/ 		// Return the exports of the module
/******/ 		return module.exports;
/******/ 	}
/******/ 	
/************************************************************************/
/******/ 	/* webpack/runtime/define property getters */
/******/ 	(() => {
/******/ 		// define getter functions for harmony exports
/******/ 		__webpack_require__.d = (exports, definition) => {
/******/ 			for(var key in definition) {
/******/ 				if(__webpack_require__.o(definition, key) && !__webpack_require__.o(exports, key)) {
/******/ 					Object.defineProperty(exports, key, { enumerable: true, get: definition[key] });
/******/ 				}
/******/ 			}
/******/ 		};
/******/ 	})();
/******/ 	
/******/ 	/* webpack/runtime/hasOwnProperty shorthand */
/******/ 	(() => {
/******/ 		__webpack_require__.o = (obj, prop) => (Object.prototype.hasOwnProperty.call(obj, prop))
/******/ 	})();
/******/ 	
/******/ 	/* webpack/runtime/make namespace object */
/******/ 	(() => {
/******/ 		// define __esModule on exports
/******/ 		__webpack_require__.r = (exports) => {
/******/ 			if(typeof Symbol !== 'undefined' && Symbol.toStringTag) {
/******/ 				Object.defineProperty(exports, Symbol.toStringTag, { value: 'Module' });
/******/ 			}
/******/ 			Object.defineProperty(exports, '__esModule', { value: true });
/******/ 		};
/******/ 	})();
/******/ 	
/************************************************************************/
/******/ 	
/******/ 	// startup
/******/ 	// Load entry module and return exports
/******/ 	// This entry module can't be inlined because the eval-source-map devtool is used.
/******/ 	var __webpack_exports__ = __webpack_require__("./assets/markdown-editor/index.js");
/******/ 	
/******/ })()
;