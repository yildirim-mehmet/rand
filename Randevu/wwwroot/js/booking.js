(async function () {
    const { salonId, mondayIso, chairCount, csrf } = window.bookingConfig;

    const gridEl = document.getElementById("grid");
    const myEl = document.getElementById("myAppointments");

    // Status renkleri
    function clsFor(status) {
        if (status === "Active") return "slot active";
        if (status === "Booked") return "slot booked";
        if (status === "Closed") return "slot closed";
        return "slot";
    }

    // Slot key
    function keyOf(s) { return `${s.date}|${s.startTime}|${s.chairNo}`; }

    const slotMap = new Map();

    function groupByDateAndTime(slots) {
        const map = new Map(); // date|time => array of chairs
        for (const s of slots) {
            const k = `${s.date}|${s.startTime}`;
            if (!map.has(k)) map.set(k, []);
            map.get(k).push(s);
        }
        return map;
    }

    function render(snapshot) {
        // My appointments list
        myEl.innerHTML = "<h3>Alınan Randevularım</h3>" +
            (snapshot.myAppointments.length
                ? "<ul>" + snapshot.myAppointments.map(a =>
                    `<li>#${a.id} - ${a.date} ${a.startTime} (Koltuk ${a.chairNo})
              <button data-cancel="${a.id}">İptal</button></li>`).join("") + "</ul>"
                : "<div>Yok</div>");

        myEl.querySelectorAll("button[data-cancel]").forEach(btn => {
            btn.addEventListener("click", async () => {
                const id = Number(btn.getAttribute("data-cancel"));
                if (!confirm("Bu randevunuzu iptal etmek istiyor musunuz?")) return;
                const res = await fetch("/api/booking/cancel", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        "X-CSRF-TOKEN": csrf
                    },
                    body: JSON.stringify({ appointmentId: id })
                });
                const txt = await res.text();
                if (!res.ok) { alert(txt); return; }
                await loadSnapshot();
            });
        });

        // slot map
        slotMap.clear();
        snapshot.slots.forEach(s => slotMap.set(keyOf(s), s));

        // tablo (satır: saat, sütun: gün+koltuk)
        // Snapshot'ta date listesi var: monday..sunday
        const dates = [];
        {
            const start = new Date(snapshot.monday + "T00:00:00");
            for (let i = 0; i < 7; i++) {
                const d = new Date(start);
                d.setDate(start.getDate() + i);
                const iso = d.toISOString().slice(0, 10);
                dates.push(iso);
            }
        }

        // saat listesi snapshot'tan türetelim
        const times = Array.from(new Set(snapshot.slots.map(s => s.startTime))).sort();

        let html = `<table class="tbl">
      <thead>
        <tr>
          <th>Saat</th>
          ${dates.map(d => `<th colspan="${chairCount}">${d}</th>`).join("")}
        </tr>
        <tr>
          <th></th>
          ${dates.map(_ => Array.from({ length: chairCount }, (_, i) => `<th>K${i + 1}</th>`).join("")).join("")}
        </tr>
      </thead>
      <tbody>`;

        for (const t of times) {
            html += `<tr><td><b>${t}</b></td>`;
            for (const d of dates) {
                for (let c = 1; c <= chairCount; c++) {
                    const s = slotMap.get(`${d}|${t}|${c}`);
                    const st = s ? s.status : "Closed";
                    const title = s && s.isMine && s.displayName ? `Ben: ${s.displayName}` : "";
                    const data = s ? `data-date="${s.date}" data-time="${s.startTime}" data-chair="${s.chairNo}" data-status="${s.status}" data-mine="${s.isMine}" data-appt="${s.appointmentId || ""}"` : "";
                    html += `<td class="${clsFor(st)}" title="${title}" ${data}></td>`;
                }
            }
            html += `</tr>`;
        }

        html += `</tbody></table>`;
        gridEl.innerHTML = html;

        // Click handling
        gridEl.querySelectorAll("td.slot").forEach(td => {
            td.addEventListener("click", async () => {
                const status = td.getAttribute("data-status");
                const date = td.getAttribute("data-date");
                const time = td.getAttribute("data-time");
                const chair = td.getAttribute("data-chair");
                const isMine = td.getAttribute("data-mine") === "true";
                const apptId = td.getAttribute("data-appt");

                if (!date || !time || !chair) return;

                if (status === "Active") {
                    if (!confirm(`${date} ${time} - Koltuk ${chair} randevuyu almak istiyor musunuz?`)) return;

                    const res = await fetch("/api/booking/book", {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                            "X-CSRF-TOKEN": csrf
                        },
                        body: JSON.stringify({
                            salonId: salonId,
                            chairNo: Number(chair),
                            date: date,
                            startTime: time
                        })
                    });

                    const txt = await res.text();
                    if (!res.ok) { alert(txt); return; }
                    await loadSnapshot();
                    return;
                }

                if (status === "Booked" && isMine && apptId) {
                    if (!confirm("Bu randevunuzu iptal etmek istiyor musunuz?")) return;

                    const res = await fetch("/api/booking/cancel", {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                            "X-CSRF-TOKEN": csrf
                        },
                        body: JSON.stringify({ appointmentId: Number(apptId) })
                    });

                    const txt = await res.text();
                    if (!res.ok) { alert(txt); return; }
                    await loadSnapshot();
                    return;
                }
            });
        });
    }

    async function loadSnapshot() {
        const res = await fetch(`/api/booking/week/${salonId}`);
        if (!res.ok) {
            gridEl.innerHTML = "Snapshot alınamadı.";
            return;
        }
        const data = await res.json();
        render(data);
    }

    // SignalR
    const conn = new signalR.HubConnectionBuilder()
        .withUrl("/bookingHub")
        .withAutomaticReconnect()
        .build();

    conn.on("SlotChanged", () => {
        // Güvenlik + tutarlılık için: delta yerine full refresh
        loadSnapshot();
    });

    await conn.start();
    await conn.invoke("JoinWeekGroup", salonId, mondayIso);

    await loadSnapshot();
})();
