(function () {
    const page=document.getElementById('friendDuel');
    if(!page?.dataset.id || ![0,1].includes(Number(page.dataset.status))) return;
    const countdown=document.getElementById('friendDuelCountdown'), error=document.getElementById('friendDuelConnection');
    let deadline=Date.parse(page.dataset.expires), busy=false, stopped=false;
    function tick() {
        const seconds=Math.max(0,Math.ceil((deadline-Date.now())/1000));
        countdown.textContent=`${String(Math.floor(seconds/60)).padStart(2,'0')}:${String(seconds%60).padStart(2,'0')}`;
    }
    async function poll() {
        if(busy||stopped) return; busy=true;
        try {
            const response=await fetch(`/Friends/Duel/${page.dataset.id}/Status`,{cache:'no-store'});
            if(!response.ok) throw new Error('Connexion indisponible');
            const data=await response.json();error.hidden=true;
            if(data.status===2 && data.matchId) {stopped=true;window.location.assign(`/Game/Play/${data.matchId}`);return;}
            if(data.status!==Number(page.dataset.status)) {stopped=true;window.location.reload();return;}
            deadline=Date.now()+Math.max(0,Date.parse(data.expiresAt)-Date.parse(data.serverTime));tick();
        } catch {error.hidden=false;} finally {busy=false;}
    }
    tick();poll();
    const clock=window.setInterval(tick,250), polling=window.setInterval(poll,2000);
    window.addEventListener('pagehide',()=>{stopped=true;window.clearInterval(clock);window.clearInterval(polling);});
})();
