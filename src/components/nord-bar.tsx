import {For, Show, onCleanup, onMount, createSignal} from 'solid-js';
import {Dynamic} from 'solid-js/web';
import {barLayout,riceWidgets,WidgetKey} from '../lib/rice-widgets';
import Glazewm from './bar/glazewm';
import Direction from './bar/direction';
import Spotlight from './bar/spotlight';
import WorkspaceRules from './bar/workspace-rules';
import Volume from './bar/volume';
import {Player} from './bar/media';
import Tray from './bar/tray';
import Memory from './bar/memory';
import Cpu from './bar/cpu';
import Vpn from './bar/vpn';
import Power from './bar/power';
import Datetime from './bar/datetime';
import WorkspaceTasks from './bar/workspace-tasks';
import QuickActions from './bar/quick-actions';
import NotificationBell from './bar/notification-bell';
const components={notifications:NotificationBell,quickActions:QuickActions,workspaces:Glazewm,direction:Direction,search:Spotlight,rules:WorkspaceRules,volume:Volume,spotify:()=> <div class="nord-players"><Player source="spotify" label="Spotify" /></div>,helium:()=> <div class="nord-players"><Player source="helium" label="Helium" /></div>,tray:Tray,memory:Memory,cpu:Cpu,vpn:Vpn,power:Power};
export default function NordBar(){
 let clock!:HTMLDivElement;const [clockWidth,setClockWidth]=createSignal(248);
 onMount(()=>{const observer=new ResizeObserver(()=>setClockWidth(clock.getBoundingClientRect().width));observer.observe(clock);onCleanup(()=>observer.disconnect());});
 const lane=(keys:()=>string[],side:string)=><div class={'nord-layout-lane nord-layout-'+side} onWheel={e=>{const lane=e.currentTarget;if(lane.scrollWidth>lane.clientWidth&&Math.abs(e.deltaY)>Math.abs(e.deltaX)){lane.scrollLeft+=e.deltaY;e.preventDefault();}}}><div class="nord-layout-items"><For each={keys()}>{key=><Show when={riceWidgets()[key as WidgetKey]}><div class="nord-layout-slot" data-widget={key}><Dynamic component={components[key as keyof typeof components]} /></div></Show>}</For></div></div>;
 return <div class="nord-frost-bar nord-arranged" style={{'--clock-width':clockWidth()+'px'}}>
  <svg class="nord-horizontal-edge" aria-hidden="true"><defs><linearGradient id="nord-horizontal-gradient" x1="0%" x2="100%"><stop offset="0%"/><stop offset="100%"/></linearGradient></defs><rect/></svg>
  {lane(()=>barLayout().left,'left')}
  <WorkspaceTasks />
  <div ref={clock} class="nord-layout-clock"><Datetime /></div>
  {lane(()=>barLayout().right,'right')}
 </div>;
}
