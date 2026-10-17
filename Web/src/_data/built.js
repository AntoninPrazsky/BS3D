// When this build was made, for the footer's "Last updated". publish.ps1 builds a clean _site and uploads it at once, so
// this is the day of the last publish to the hosting (docs/website.md). The day is the owner's (Prague), not UTC's.
const now = new Date();

module.exports = {
    iso: now.toISOString(),
    text: now.toLocaleDateString("en-GB", { day: "numeric", month: "long", year: "numeric", timeZone: "Europe/Prague" })
};
