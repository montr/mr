const path = require("path");
const webpack = require("webpack");
const HtmlWebpackPlugin = require("html-webpack-plugin");
const MonacoWebpackPlugin = require("monaco-editor-webpack-plugin");
const packageMetadata = require("./package.json");

module.exports = {
  entry: "./src/index.tsx",
  output: {
    path: path.resolve(__dirname, "dist"),
    filename: "assets/[name].[contenthash].js",
    clean: true,
    publicPath: "/"
  },
  resolve: { extensions: [".tsx", ".ts", ".js"] },
  module: {
    rules: [
      { test: /\.tsx?$/, exclude: /node_modules/, use: "swc-loader" },
      { test: /\.css$/, use: ["style-loader", "css-loader"] },
      { test: /\.ttf$/, type: "asset/resource" }
    ]
  },
  plugins: [
    new HtmlWebpackPlugin({ template: "./public/index.html" }),
    new MonacoWebpackPlugin({ languages: ["json"] }),
    new webpack.DefinePlugin({ __APP_VERSION__: JSON.stringify(packageMetadata.version) })
  ],
  devServer: {
    port: 3000,
    historyApiFallback: true,
    proxy: [{ context: ["/api", "/docs", "/openapi"], target: process.env.MONOREPORT_API_URL || "http://127.0.0.1:5080" }]
  },
  performance: { hints: false }
};
