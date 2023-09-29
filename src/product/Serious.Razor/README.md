# Serious Razor Class Library

This is a Razor Class Library containing reusable Razor components for future ASP.NET Core goodness.

## Markdown Editor Tag Helper

This tag helper provides a nice textarea to edit markdown complete with markdown buttons and preview. 


### Example usage

```html
<markdowneditor asp-for="Input.ReleaseNotes" placeholder="Tell the world what’s changed." />
```

### Setup

Unfortunately, this isn't a drop in and use component. There are a few steps to getting it set up.

1. In the target web application, make sure the following NPM packages are installed.

* `@github/markdown-toolbar-element`
* `@github/tab-container-element`
* `@github/textarea-autosize`

2. Make sure the following JS and CSS files are referenced. Typically in a section.

```html
@section Styles {
    <link href="https://fonts.googleapis.com/icon?family=Material+Icons" rel="stylesheet">
    <link rel="stylesheet" href="~/_content/Serious.Razor/dist/css/markdown-editor.css">
    <script src="~/_content/Serious.Razor/dist/js/markdown-editor.js"></script>
}
```

And that _should_ do it!