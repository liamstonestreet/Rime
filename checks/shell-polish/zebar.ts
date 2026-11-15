// Synthetic provider data: this fixture never connects to Windows or a real helper.
const command = async (value: unknown) => { (window as any).shellCalls.push(value); };
const workspaces = ['1','2','3','4','5','6','7','8','9'].map((name,index)=>({id:name,name,displayName:name,hasFocus:index===0,type:'workspace',children:[]}));
workspaces[0].children.push({id:'sample-app',handle:100,type:'window',title:'Discord',processName:'Discord',hasFocus:true} as never);
const monitor = {id:'display',x:0,y:0,width:1920,height:1080};
const glaze = {currentWorkspaces:workspaces,allWorkspaces:workspaces,currentMonitor:monitor,allMonitors:[monitor],displayedWorkspace:workspaces[0],focusedWorkspace:workspaces[0],tilingDirection:'horizontal',runCommand:command};
const sessions = [{sessionId:'Spotify.exe',title:'See You Again',artist:'Hevi',isPlaying:true,position:42,startTime:0,endTime:240},{sessionId:'Helium.test',title:'Reverse Engineering',artist:'Low Level',isPlaying:false,position:33,startTime:0,endTime:600}];
const mediaListeners=new Set<(value:unknown)=>void>();
let mediaOutput={allSessions:sessions,previous:command,next:command,togglePlayPause:command};
(window as any).fixtureMedia=(active:boolean)=>{mediaOutput={...mediaOutput,allSessions:active?sessions:[]};for(const listener of mediaListeners)listener(mediaOutput);};
export function createProvider(config: {type:string}) {
 const output = config.type==='glazewm'?glaze:config.type==='cpu'?{usage:18}:config.type==='memory'?{usedMemory:12e9,totalMemory:32e9}:config.type==='systray'?{icons:[]}:mediaOutput;
 return {output,onOutput:(listener:(value:unknown)=>void)=>{if(config.type==='media')mediaListeners.add(listener);return()=>mediaListeners.delete(listener);},stop:async()=>{}};
}
export const shellExec=command;
export function currentWidget(){return {configPath:'C:/fixture/zpack.json',htmlPath:'C:/fixture/dist/glazewm/glazewm.html',tauriWindow:{outerPosition:async()=>({x:0,y:0}),setFocus:async()=>{}}};}
