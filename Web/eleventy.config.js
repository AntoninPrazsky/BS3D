// The landing page of Bubble Shooter 3D (#815): Eleventy turns src/ into plain files in _site/, which is all the
// Webglobe hosting serves. How it is built and published: docs/website.md.
module.exports = function (eleventyConfig) {
    eleventyConfig.addPassthroughCopy("src/css");
    eleventyConfig.addPassthroughCopy("src/fonts");
    eleventyConfig.addPassthroughCopy("src/img");

    // A post's date as a reader says it, for the blog (src/blog/*.md, tagged "posts" by blog.11tydata.json)
    eleventyConfig.addFilter("readableDate", (date) =>
        date.toLocaleDateString("en-GB", { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" }));

    return { dir: { input: "src", output: "_site" } };
};
