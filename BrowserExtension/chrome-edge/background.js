const MENU_ID = "sekodl-download-link";
const HOST_NAME = "com.sekodl.bridge";

chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.create({
    id: MENU_ID,
    title: "Download link with SekoDL",
    contexts: ["link"]
  });
});

chrome.contextMenus.onClicked.addListener((info) => {
  if (info.menuItemId !== MENU_ID || !info.linkUrl) {
    return;
  }

  chrome.runtime.sendNativeMessage(
    HOST_NAME,
    {
      action: "enqueue",
      url: info.linkUrl
    },
    () => {
      void chrome.runtime.lastError;
    }
  );
});
