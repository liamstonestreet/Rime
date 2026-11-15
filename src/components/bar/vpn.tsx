import {createSignal} from 'solid-js';
import {vpnStatus,openVpnSettings} from '../../lib/rice-widgets';
export default function Vpn(){
 const [error,setError]=createSignal('');
 const title=()=>error()||vpnStatus().error||(vpnStatus().connected?'Active VPN adapter: '+vpnStatus().names.join(', ')+'. Click for VPN settings.':'No active VPN adapter detected. Click for VPN settings.');
 return <button type="button" class="nord-vpn" classList={{connected:vpnStatus().connected}} title={title()} aria-label={title()} onDblClick={e=>e.stopPropagation()} onClick={()=>setError(openVpnSettings()?'':'Control center is reconnecting. Try again.')}>
  <svg viewBox="0 0 24 24" width="17" height="17" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><path d="M12 3 4.5 6v6c0 4 4 7 7.5 9 3.5-2 7.5-5 7.5-9V6Z" stroke-linejoin="round"/><path d={vpnStatus().connected?'m8 12 3 3 5-6':'M9 12h6'} stroke-linecap="round" stroke-linejoin="round"/></svg>
  <span>{vpnStatus().connected?'VPN on':'VPN off'}</span>
 </button>;
}
