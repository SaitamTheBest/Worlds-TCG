(() => {
    'use strict';
    const page=document.querySelector('.friends-page'); if(!page) return;
    const stylesheet=[...document.querySelectorAll('link[rel="stylesheet"]')].find(link=>new URL(link.href,document.baseURI).pathname.endsWith('/css/card.css'));
    if(stylesheet&&stylesheet.parentElement!==document.head) document.head.appendChild(stylesheet);
    window.worldsCardComponents?.hydrate(page);
    const thread=page.querySelector('[data-friends-thread]');
    let refreshRequest;
    async function refresh() {
        if(!thread||document.hidden) return;
        refreshRequest?.abort(); const current=new AbortController(); refreshRequest=current;
        const old=thread.querySelector('[data-message-list]'); const atBottom=!old||old.scrollHeight-old.scrollTop-old.clientHeight<40; const position=old?.scrollTop||0;
        try {
            const response=await fetch(page.dataset.conversationUrl,{signal:current.signal});
            if(!response.ok||response.redirected) return;
            const html=await response.text(); if(current!==refreshRequest) return;
            thread.innerHTML=html; const list=thread.querySelector('[data-message-list]');
            if(list) list.scrollTop=atBottom?list.scrollHeight:position;
        } catch(error) { if(!current.signal.aborted) console.warn('Conversation momentanément indisponible'); }
    }
    const list=thread?.querySelector('[data-message-list]'); if(list) list.scrollTop=list.scrollHeight;
    const form=page.querySelector('[data-friend-message]');
    form?.addEventListener('submit',async event=> {
        event.preventDefault(); const button=form.querySelector('button[type=submit]'); if(button.disabled) return;
        button.disabled=true; const error=form.querySelector('[data-message-error]'); error.hidden=true;
        try {
            const response=await fetch(form.action,{method:'POST',body:new FormData(form)});
            if(!response.ok||response.redirected) { const result=await response.json().catch(()=>({})); throw new Error(result.message||'Le message n’a pas pu être envoyé.'); }
            form.querySelector('textarea').value=''; await refresh();
        } catch(ex) {error.textContent=ex.message;error.hidden=false;} finally {button.disabled=false;}
    });
    if(thread) setInterval(refresh,15000);
    if(!window.signalR||!page.hasAttribute('data-friends-page')) return;
    const connection=new signalR.HubConnectionBuilder().withUrl('/hubs/friends').withAutomaticReconnect().build();
    connection.on('FriendsChanged',()=> {if(thread) refresh(); else {const banner=page.querySelector('[data-friends-refresh]'); if(banner) banner.hidden=false;} });
    connection.onreconnected(()=> {if(thread) refresh();});
    connection.start().catch(()=>console.warn('Mise à jour en direct des amis indisponible'));
    window.addEventListener('pagehide',()=>connection.stop(),{once:true});
})();
