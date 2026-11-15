import { packCommand } from '../../lib/pack';
import {openPowerMenu} from "../../lib/rice-widgets";
import { useProviders } from '../../lib/providers-context';
import { toScreen } from '../../lib/screen';
export default function Power(){const {glazewm}=useProviders();return <button class="nord-power" title="Power" aria-label="Open power menu" onDblClick={e=>e.stopPropagation()} onClick={e=>{e.stopPropagation();const r=e.currentTarget.getBoundingClientRect(),p=toScreen(r.left+r.width/2,r.top);if(openPowerMenu(p.x,p.y))return;void glazewm()?.runCommand(`shell-exec ${packCommand('NordControlCenter.exe')} --power ${p.x} ${p.y}`);}}><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"><path d="M12 3v8M6.3 5.8a8.5 8.5 0 1 0 11.4 0"/></svg></button>}
