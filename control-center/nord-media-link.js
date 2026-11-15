// Reports current Spotify metadata and canonical track/playlist/album links locally.
(async function nordMediaLink(){
 while(!globalThis.Spicetify?.Player?.addEventListener)await new Promise(r=>setTimeout(r,500));
 let socket,retry,shuffleRefresh;
 const publish=()=>{
  const data=Spicetify.Player.data, item=data?.item;
  const match=/^spotify:(track|episode):([A-Za-z0-9]+)$/.exec(item?.uri||'');if(!match||socket?.readyState!==1)return;
  const state=Spicetify.Platform?.PlayerAPI?._state;
  const contextUri=data.context_uri||data.context?.uri||state?.context?.uri||'';
  const contextMeta=data.context_metadata||data.context?.metadata||state?.context?.metadata||{};
  let playlist='';
  if(/^spotify:(?:user:[^:]+:)?playlist:/.test(contextUri)){
   playlist=contextMeta.context_description||contextMeta.name||contextMeta.title||data.context?.name||'';
   if(!playlist){const id=contextUri.split(':').pop();const link=[...document.querySelectorAll('a[href]')].find(a=>a.getAttribute('href')===`/playlist/${id}`&&a.textContent?.trim());playlist=link?.textContent?.trim()||'';}
  }
  const spotifyLink=(uri,kind)=>{const match=new RegExp(`^spotify:(?:user:[^:]+:)?${kind}:([A-Za-z0-9]+)$`).exec(uri||'');return match?`https://open.spotify.com/${kind}/${match[1]}`:'';};
  const albumUrl=spotifyLink(item.metadata?.album_uri||item.album?.uri||(contextUri.startsWith('spotify:album:')?contextUri:''),'album');
  const playlistUrl=spotifyLink(contextUri,'playlist');
  const artistUrl=spotifyLink(item.metadata?.artist_uri||item.artists?.[0]?.uri,'artist');
  const shuffle=typeof Spicetify.Player.getShuffle==='function'&&typeof Spicetify.Player.setShuffle==='function'?String(Spicetify.Player.getShuffle()):'';
  socket.send(JSON.stringify({artistUrl,shuffle,albumUrl,playlistUrl,url:`https://open.spotify.com/${match[1]}/${match[2]}`,title:item.metadata?.title||item.name||'',album:item.metadata?.album_title||item.album?.name||'',playlist}));
 };
 const connect=()=>{socket=new WebSocket('ws://127.0.0.1:6138/__NORD_CENTER_TOKEN__');socket.onopen=()=>{socket.send('subscribe');publish();};socket.onmessage=event=>{try{const command=JSON.parse(event.data).mediaControl;if(command?.action!=='shuffle'||typeof command.value!=='boolean'||typeof Spicetify.Player.setShuffle!=='function')return;const uri=Spicetify.Player.data?.item?.uri||'';const current=/^spotify:(track|episode):([A-Za-z0-9]+)$/.exec(uri);if(!current||command.url!==`https://open.spotify.com/${current[1]}/${current[2]}`)return;Spicetify.Player.setShuffle(command.value);clearTimeout(shuffleRefresh);shuffleRefresh=setTimeout(publish,800);}catch{}};socket.onerror=()=>{};socket.onclose=()=>{clearTimeout(shuffleRefresh);clearTimeout(retry);retry=setTimeout(connect,3000);};};
 Spicetify.Player.addEventListener('songchange',publish);Spicetify.Player.addEventListener('onplaypause',publish);setInterval(publish,15000);connect();
})();
