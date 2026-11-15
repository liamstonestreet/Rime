import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import vm from 'node:vm';

test('Spotify extension reports song, playlist and album URLs without inventing unavailable links',()=>{
  const sent=[],handlers={},shuffleChanges=[];let socket;let shuffled=false;
  const Player={getShuffle:()=>shuffled,setShuffle:value=>{shuffled=value;shuffleChanges.push(value);},data:{item:{uri:'spotify:track:Song123',metadata:{title:'Song',album_uri:'spotify:album:Album123',album_title:'Album',artist_uri:'spotify:artist:Artist123'}},context_uri:'spotify:user:owner:playlist:Playlist123',context_metadata:{name:'Playlist'}},addEventListener:(name,fn)=>handlers[name]=fn};
  const sandbox={Spicetify:{Player},document:{querySelectorAll:()=>[]},setInterval(){},clearTimeout(){},setTimeout(){},WebSocket:class{readyState=1;constructor(){socket=this;}send(payload){if(payload!=='subscribe')sent.push(JSON.parse(payload));}}};
  vm.runInNewContext(readFileSync(new URL('../control-center/nord-media-link.js',import.meta.url),'utf8'),sandbox);
  socket.onopen();assert.equal(sent[0].url,'https://open.spotify.com/track/Song123');assert.equal(sent[0].playlistUrl,'https://open.spotify.com/playlist/Playlist123');assert.equal(sent[0].albumUrl,'https://open.spotify.com/album/Album123');
  assert.equal(sent[0].artistUrl,'https://open.spotify.com/artist/Artist123');assert.equal(sent[0].shuffle,'false');
  socket.onmessage({data:JSON.stringify({mediaControl:{action:'shuffle',url:sent[0].url,value:true}})});assert.deepEqual(shuffleChanges,[true]);
  socket.onmessage({data:JSON.stringify({mediaControl:{action:'shuffle',url:'https://open.spotify.com/track/other',value:false}})});assert.deepEqual(shuffleChanges,[true],'stale-track shuffle must not affect the new track');
  socket.onmessage({data:JSON.stringify({mediaControl:{action:'shuffle',url:sent[0].url,value:'false'}})});assert.deepEqual(shuffleChanges,[true],'malformed shuffle must not change playback');
  Player.data={item:{uri:'spotify:episode:Episode123',metadata:{title:'Episode'}},context_uri:'spotify:show:Show123'};
  handlers.songchange();assert.equal(sent[1].playlistUrl,'');assert.equal(sent[1].albumUrl,'');assert.equal(sent[1].url,'https://open.spotify.com/episode/Episode123');
  Player.data={item:{uri:'spotify:track:Song456',metadata:{title:'Direct album'}},context_uri:'spotify:album:Album456'};
  handlers.songchange();assert.equal(sent[2].albumUrl,'https://open.spotify.com/album/Album456');assert.equal(sent[2].playlistUrl,'');
});
