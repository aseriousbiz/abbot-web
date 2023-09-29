import './index.scss';
import setupMarkdownTextAreas from './js/markdown-editor';

document.addEventListener("DOMContentLoaded", function() {
    // Apply Markdown Text Area.
    setupMarkdownTextAreas(document);
});

// Support Turbo, if present.
document.addEventListener("turbo:render", function() {
    // Apply Markdown Text Area.
    setupMarkdownTextAreas(document);
});