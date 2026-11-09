function selectTab(activeId) {
    var tabs = ['TcHmiButton_5', 'TcHmiButton_6', 'TcHmiButton_7', 'TcHmiButton_9'];
    var icons = ['TcHmiImage_14', 'TcHmiImage_10', 'TcHmiImage_11', 'TcHmiImage_12'];
    tabs.forEach(function (id, index) {
        var btn = TcHmi.Controls.get(id);
        var icon = TcHmi.Controls.get(icons[index]);
        if (!btn) return;

        if (id === activeId) {
            btn.setBackgroundColor({ color: 'rgba(26, 54, 93, 1)' });
            btn.setTextColor({ color: 'rgba(255, 255, 255, 1)' });
            if (icon) icon.setOpacity(1.0);
        } else {
            btn.setBackgroundColor({ color: 'rgba(74, 111, 110, 1)' });
            btn.setTextColor({ color: 'rgba(255, 255, 255, 0.7)' });
            if (icon) icon.setOpacity(0.45);
        }
    });
}