import { bridgeTokens } from './install-config.generated';
// Local search helper (NordSpotlight.exe). The token is generated per install by install.ps1.
export const spotlightUrl = "ws://127.0.0.1:6137/" + bridgeTokens.spotlight;
