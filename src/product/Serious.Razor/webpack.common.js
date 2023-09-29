const path = require('path')
const { CleanWebpackPlugin } = require("clean-webpack-plugin");
const MiniCssExtractPlugin = require("mini-css-extract-plugin");

module.exports = {
    target: 'web',
    entry: {
        'markdown-editor': './assets/markdown-editor/index.js'
    },
    output: {
       publicPath: "/wwwroot/",
       path: path.join(__dirname, '/wwwroot/dist/js'),
       filename: '[name].js'
    },
    devtool: 'source-map',
    module: {
        rules: [
            {
                test: /\.m?js$/,
                use: {
                    loader: 'babel-loader',
                    options: {
                        presets: [
                            [
                                '@babel/preset-env',
                                {
                                    targets: {
                                        esmodules: true,
                                    }
                                },
                            ],
                        ],
                        plugins: ['@babel/plugin-proposal-nullish-coalescing-operator']
                    }
                }
            },
            {
                test: /\.node$/,
                use: 'node-loader'
            },
            {
                test: /\.s?css$/i,
                use: [
                    // Creates `style` nodes from JS strings
                    MiniCssExtractPlugin.loader,
                    // Translates CSS into CommonJS
                    'css-loader',
                    // Compiles Sass to CSS
                    'sass-loader',
                ]
            }
        ]
    },
    plugins: [
        new CleanWebpackPlugin({cleanOnceBeforeBuildPatterns: ["wwwroot/dist/*"]}),
        new MiniCssExtractPlugin({
          filename: "../css/[name].css"
          /* filename: "../css/[name].[chunkhash].css" */
        })
    ]
};