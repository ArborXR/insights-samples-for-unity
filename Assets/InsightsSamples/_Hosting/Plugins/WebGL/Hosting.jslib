mergeInto(LibraryManager.library, {
    HostingClearLink: function () {
        history.replaceState(null, "", location.pathname + location.search);
    },
    HostingRelaunch: function (fragment) {
        location.hash = UTF8ToString(fragment);
        location.reload();
    },
    HostingWatchLink: function (key) {
        var marker = UTF8ToString(key) + "=";
        window.addEventListener("hashchange", function () {
            if (location.hash.indexOf(marker) >= 0) location.reload();
        });
    },
});
