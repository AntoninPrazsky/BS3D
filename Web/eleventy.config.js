// The landing page and the blog of Bubble Shooter 3D (#815, #928): Eleventy turns src/ into plain files in _site/, which
// is all the Webglobe hosting serves. How it is built and published: docs/website.md.
const site = require("./src/_data/site.json");

module.exports = function (eleventyConfig) {
    eleventyConfig.addPassthroughCopy("src/css");
    eleventyConfig.addPassthroughCopy("src/fonts");
    eleventyConfig.addPassthroughCopy("src/img");
    // The live cannon (#949): its script and the game's meshes Tools/WindingCheck --export-cannon writes
    eleventyConfig.addPassthroughCopy("src/js");
    eleventyConfig.addPassthroughCopy("src/cannon");
    // And the game's sound behind the page's toggle (#950): the game's own files, copied at the build, so the page never keeps
    // a copy that could fall behind them
    eleventyConfig.addPassthroughCopy({ "../Game/Music/menu.ogg": "audio/menu.ogg", "../Game/Sfx/shoot.ogg": "audio/shoot.ogg" });

    // A post's date as a reader says it, for the blog (src/blog/*.md, tagged "posts" by blog.11tydata.json)
    eleventyConfig.addFilter("readableDate", (date) =>
        date.toLocaleDateString("en-GB", { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" }));

    // A date as the Atom feed wants it (RFC 3339, UTC)
    eleventyConfig.addFilter("isoDate", (date) => date.toISOString());

    // A post's HTML for the feed: a feed reader shows it away from this site, so every root-relative link and picture
    // is given the site's address
    eleventyConfig.addFilter("absoluteLinks", (html) =>
        String(html)
            .replace(/(href|src)="\/(?!\/)/g, `$1="${site.url}/`)
            .replace(/srcset="([^"]*)"/g, (_, list) => `srcset="${list.replace(/(^|,\s*)\/(?!\/)/g, `$1${site.url}/`)}"`));

    // A post is Markdown and nothing else: no Liquid pass over it, so a brace in a code sample is only a brace
    return { dir: { input: "src", output: "_site" }, markdownTemplateEngine: false };
};
