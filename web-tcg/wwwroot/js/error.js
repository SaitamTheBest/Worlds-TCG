(() => {
    document.getElementById('retry')?.addEventListener('click', () => {
        if (window.location.pathname.toLowerCase().startsWith('/error')) window.location.assign('/');
        else window.location.reload();
    });
})();
