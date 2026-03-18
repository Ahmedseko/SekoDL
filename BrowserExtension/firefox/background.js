const MENU_ID = "sekodl-download-link";
const HOST_NAME = "com.sekodl.bridge";

browser.runtime.onInstalled.addListener(() => {
  browser.contextMenus.create({
    id: MENU_ID,
    title: "Download link with SekoDL",
    contexts: ["link"]
  });
});

browser.contextMenus.onClicked.addListener((info) => {
  if (info.menuItemId !== MENU_ID || !info.linkUrl) {
    return;
  }

  browser.runtime.sendNativeMessage(HOST_NAME, {
    action: "enqueue",
    url: info.linkUrl
  }).catch(() => {});
});
