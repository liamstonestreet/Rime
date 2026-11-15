import {notificationBadge,sendCenter} from '../../lib/rice-widgets';
import {refreshBarOrigin,toScreen} from '../../lib/screen';
import {Show} from 'solid-js';
export default function NotificationBell(){
 const toggle=async(element:HTMLButtonElement)=>{await refreshBarOrigin();const r=element.getBoundingClientRect(),a=toScreen(r.left,r.top),b=toScreen(r.right,r.bottom);sendCenter(`notifications-open:pin,${a.x},${a.y},${b.x},${b.y},${Math.round(window.devicePixelRatio*1000)},${toScreen(0,document.querySelector('.nord-frost-bar')?.getBoundingClientRect().bottom??50).y}`);};
 return <button class="nord-quick-action nord-notification-bell" type="button" title={'Notifications ? '+notificationBadge().unread+' unread'} aria-label={'Open notifications, '+notificationBadge().unread+' unread'} onClick={e=>void toggle(e.currentTarget)}><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M6 9a6 6 0 0 1 12 0c0 7 3 7 3 9H3c0-2 3-2 3-9M9 21h6M12 1v2"/></svg><Show when={notificationBadge().unread>0}><span class="nord-notification-count">{notificationBadge().unread>99?'99+':notificationBadge().unread}</span></Show></button>;
}
