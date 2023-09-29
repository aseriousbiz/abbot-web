module.exports = {
    content: [
       './Pages/**/*.{html,cshtml}',
       './assets/**/*.{ts,tsx,js}',
       './Infrastructure/TagHelpers/**/*.cs'
    ],
    blocklist: [
        "hover:border",
    ],
    theme: {
        extend: {
            minWidth: theme => ({
                80: theme('spacing[80]'),
            }),
            fontFamily: {
                sans: [
                    '"Work Sans"',
                    'system-ui',
                    '-apple-system',
                    'BlinkMacSystemFont',
                    '"Segoe UI"',
                    'Roboto',
                    '"Helvetica Neue"',
                    'Arial',
                    '"Noto Sans"',
                    'sans-serif',
                    '"Apple Color Emoji"',
                    '"Segoe UI Emoji"',
                    '"Segoe UI Symbol"',
                    '"Noto Color Emoji"',
                ],
            },
        }
    },
    plugins: [
        require('@tailwindcss/line-clamp'),
        require('tailwindcss-dotted-background'),
    ],
}
