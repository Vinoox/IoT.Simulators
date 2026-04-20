const API_BASE = '/api';

// Funkcja główna do pobierania stanu
async function fetchServicesState() {
    try {
        const response = await fetch(`${API_BASE}/registry/services`);
        if (!response.ok) throw new Error('Błąd pobierania danych');

        const services = await response.json();
        renderServices(services);
        document.getElementById('lastSyncTime').innerText = `Ostatnia synchronizacja: ${new Date().toLocaleTimeString()}`;
    } catch (error) {
        console.error('Błąd:', error);
        showAlert('Nie można nawiązać połączenia z rejestrem serwisów.', 'danger');
    }
}

// Renderowanie kart w HTML
function renderServices(services) {
    // 1. Rozszerzona blokada odświeżania UI dla inputów ORAZ list wyboru
    const activeElement = document.activeElement;
    if (activeElement && (activeElement.tagName === 'INPUT' || activeElement.tagName === 'SELECT')) {
        return;
    }

    // 2. Pobieranie obu kontenerów
    const simGrid = document.getElementById('simulatorsGrid');
    const infraGrid = document.getElementById('infraGrid');
    const loadingSpinner = document.getElementById('loadingSpinner');

    if (loadingSpinner) loadingSpinner.remove(); // Usuń spinner po załadowaniu

    simGrid.innerHTML = '';
    infraGrid.innerHTML = '';

    // Liczniki do górnych odznak
    let simCount = 0;
    let infCount = 0;

    // 3. Rysowanie kart serwisów
    services.forEach(service => {
        const isRunning = service.isRunning;
        const currentProtocol = (service.protocol || "HTTP").toUpperCase();

        // --- LOGIKA WIZUALNA DLA INFRASTRUKTURY (Broker / Collector) ---
        const isInfra = service.serviceId.includes('Broker') || service.serviceId.includes('Collector') || service.serviceId.includes('DataCollector-HTTP');

        const card = document.createElement('div');
        let cardBodyHtml = '';

        if (isInfra) {
            infCount++;
            // INFRASTRUKTURA: Pełna szerokość bocznego panelu (col-12)
            card.className = 'col-12 mb-4';

            const statusBadge = isRunning
                ? `<div class="d-flex align-items-center"><span class="status-dot"></span><span class="badge bg-success bg-opacity-10 text-success border border-success border-opacity-25 px-2 py-1">ONLINE</span></div>`
                : `<div class="d-flex align-items-center"><span class="status-dot stopped"></span><span class="badge bg-danger bg-opacity-10 text-danger border border-danger border-opacity-25 px-2 py-1">OFFLINE</span></div>`;

            cardBodyHtml = `
                <div class="card h-100 bg-dark text-white border-secondary shadow-lg">
                    <div class="card-header border-bottom border-secondary border-opacity-25 d-flex justify-content-between align-items-center">
                        <h6 class="mb-0 fw-bold text-white tracking-tight">
                            <i class="bi bi-hdd-rack-fill text-info me-2"></i>${service.serviceId.toUpperCase()}
                        </h6>
                        ${statusBadge}
                    </div>
                    <div class="card-body p-3 d-flex flex-column">
                        <div class="bg-secondary bg-opacity-25 border border-secondary rounded p-3 mb-3">
                            <p class="mb-2 border-bottom border-secondary pb-2 small d-flex justify-content-between">
                                <span class="text-white-50"><i class="bi bi-hdd-network me-1"></i> Protokół:</span> 
                                <strong class="text-white">${service.protocol}</strong>
                            </p>
                            <p class="mb-2 border-bottom border-secondary pb-2 small d-flex justify-content-between">
                                <span class="text-white-50"><i class="bi bi-ethernet me-1"></i> Host:</span> 
                                <strong class="text-white">${service.targetAddress}</strong>
                            </p>
                            <p class="mb-0 small d-flex justify-content-between">
                                <span class="text-white-50"><i class="bi bi-signpost-split me-1"></i> Port/Ścieżka:</span> 
                                <strong class="text-white text-end">${service.topicOrPath}</strong>
                            </p>
                        </div>
                        
                        <div class="mt-auto bg-black border border-success border-opacity-25 rounded p-2 d-flex justify-content-between align-items-center shadow-sm">
                            <span class="text-white-50 small"><i class="bi bi-activity text-success me-2"></i>Zebrane pakiety:</span>
                            <span class="badge bg-success text-dark fs-6 font-monospace shadow">${service.processedMessages || 0}</span>
                        </div>
                    </div>
                    <div class="card-footer bg-transparent pt-0 pb-2 border-0 text-white-50 text-end" style="font-size: 0.70rem;">
                        Odświeżono: <span class="text-white">${new Date(service.lastUpdated).toLocaleTimeString()}</span>
                    </div>
                </div>
            `;
            card.innerHTML = cardBodyHtml;
            infraGrid.appendChild(card);

        } else {
            simCount++;
            // SYMULATORY: Zmniejszone, by mieściły się 2 obok siebie na dużych ekranach i 3 na b. dużych
            card.className = 'col-md-6 col-xxl-4 mb-4';

            const statusBadge = isRunning
                ? `<div class="d-flex align-items-center"><span class="status-dot"></span><span class="badge bg-success bg-opacity-10 text-success border border-success border-opacity-25 px-2 py-1">NADAJE</span></div>`
                : `<div class="d-flex align-items-center"><span class="status-dot stopped"></span><span class="badge bg-warning bg-opacity-10 text-warning border border-warning border-opacity-25 px-2 py-1">WSTRZYMANY</span></div>`;

            cardBodyHtml = `
                <div class="card h-100 border-0 shadow-sm">
                    <div class="card-header border-bottom border-light-subtle bg-transparent d-flex justify-content-between align-items-center p-3">
                        <h5 class="mb-0 fw-bold text-dark tracking-tight">
                            <i class="bi bi-box-seam text-primary me-2 opacity-75"></i>${service.serviceId.toUpperCase()}
                        </h5>
                        ${statusBadge}
                    </div>
                    <div class="card-body d-flex flex-column p-3">
                        
                        <div class="d-flex justify-content-between align-items-center mb-3">
                            <span class="text-muted small fw-medium">Wysyłka via:</span>
                            <span class="badge bg-secondary px-3 py-1 rounded-pill shadow-sm">
                                <i class="bi ${currentProtocol.includes('HTTP') ? 'bi-globe' : 'bi-broadcast'} me-1"></i>${service.protocol}
                            </span>
                        </div>
                        
                        <div class="bg-light border p-2 mb-3 mt-auto shadow-sm rounded">
                            <h6 class="small fw-bold text-uppercase text-muted mb-2 px-1"><i class="bi bi-sliders me-1"></i> Adresacja</h6>
                            
                            <div class="mb-1">
                                <select class="form-select form-select-sm" id="protocol-${service.serviceId}">
                                    <option value="HTTP" ${currentProtocol.includes('HTTP') ? 'selected' : ''}>🌐 HTTP</option>
                                    <option value="MQTT" ${currentProtocol.includes('MQTT') ? 'selected' : ''}>📡 MQTT</option>
                                </select>
                            </div>
                            <div class="mb-1">
                                <input type="text" class="form-control form-control-sm" id="target-${service.serviceId}" value="${service.targetAddress}" placeholder="Serwer docelowy">
                            </div>
                            <div class="mb-2">
                                <input type="text" class="form-control form-control-sm" id="topic-${service.serviceId}" value="${service.topicOrPath}" placeholder="Endpoint / Topic">
                            </div>
                            <button class="btn btn-outline-dark btn-sm w-100" onclick="updateConfig('${service.serviceId}')">
                                <i class="bi bi-cloud-arrow-up-fill me-1"></i> Zastosuj
                            </button>
                        </div>

                        <div class="input-group input-group-sm mb-3 shadow-sm rounded">
                            <span class="input-group-text bg-white text-muted fw-bold border-end-0">T (ms)</span>
                            <input type="number" class="form-control text-center bg-light" id="interval-${service.serviceId}" value="${service.intervalMilliseconds}">
                            <button class="btn btn-primary px-3" onclick="updateInterval('${service.serviceId}')"><i class="bi bi-check2"></i></button>
                        </div>

                        <div class="d-grid mt-auto">
                            <button class="btn btn-${isRunning ? 'danger' : 'success'} shadow-sm py-2 text-uppercase fw-bold" 
                                    onclick="toggleState('${service.serviceId}', ${isRunning})">
                                ${isRunning ? '<i class="bi bi-stop-circle-fill me-1"></i> Zatrzymaj' : '<i class="bi bi-play-circle-fill me-1"></i> Uruchom'}
                            </button>
                        </div>
                    </div>
                </div>
            `;
            card.innerHTML = cardBodyHtml;
            simGrid.appendChild(card);
        }
    });

    // Aktualizacja liczników w UI
    document.getElementById('simulatorsCount').innerText = simCount;
    document.getElementById('infraCount').innerText = infCount;

    // Obsługa pustego stanu
    if (simCount === 0) {
        simGrid.innerHTML = `
            <div class="col-12 text-center mt-5 text-muted">
                <i class="bi bi-cpu opacity-25" style="font-size: 4rem;"></i>
                <h5 class="mt-3">Brak połączonych symulatorów</h5>
            </div>`;
    }
    if (infCount === 0) {
        infraGrid.innerHTML = `
            <div class="col-12 text-center mt-4 text-muted">
                <div class="alert alert-secondary border-0 bg-secondary bg-opacity-10 small">
                    <i class="bi bi-info-circle me-1"></i> Brak wykrytej infrastruktury sieciowej (Broker / Collector).
                </div>
            </div>`;
    }
}

// Akcje sterujące: Zapis konfiguracji sieciowej
async function updateConfig(serviceId) {
    const protocol = document.getElementById(`protocol-${serviceId}`).value;
    const target = document.getElementById(`target-${serviceId}`).value;
    const topic = document.getElementById(`topic-${serviceId}`).value;

    if (!target || !topic) {
        showAlert('Pole docelowe oraz temat/ścieżka nie mogą być puste.', 'warning');
        return;
    }

    try {
        let url = '';
        if (protocol === 'HTTP') {
            url = `${API_BASE}/control/switch-to-http?service=${serviceId}&targetAddress=${encodeURIComponent(target)}&apiPath=${encodeURIComponent(topic)}`;
        } else {
            url = `${API_BASE}/control/switch-to-mqtt?service=${serviceId}&brokerAddress=${encodeURIComponent(target)}&topic=${encodeURIComponent(topic)}`;
        }

        const response = await fetch(url, { method: 'POST' });
        if (!response.ok) throw new Error('Błąd zmiany konfiguracji');

        showAlert(`Konfiguracja sieciowa dla ${serviceId} zapisana (${protocol}).`, 'success');

        if (document.activeElement) {
            document.activeElement.blur();
        }
        fetchServicesState();
    } catch (error) {
        showAlert(`Nie udało się zmienić konfiguracji sieciowej dla ${serviceId}.`, 'danger');
    }
}

async function toggleState(serviceId, currentlyRunning) {
    const action = currentlyRunning ? 'stop' : 'start';
    try {
        const response = await fetch(`${API_BASE}/control/${action}?service=${serviceId}`, { method: 'POST' });
        if (!response.ok) throw new Error('Błąd wykonania akcji');

        showAlert(`Polecenie ${action.toUpperCase()} wysłane do ${serviceId}.`, 'success');
        fetchServicesState();
    } catch (error) {
        showAlert(`Błąd komunikacji z serwisem ${serviceId}.`, 'danger');
    }
}

async function updateInterval(serviceId) {
    const input = document.getElementById(`interval-${serviceId}`);
    const ms = parseInt(input.value, 10);

    if (isNaN(ms) || ms < 1) {
        showAlert('Wprowadź poprawną wartość interwału (min. 1 ms).', 'warning');
        return;
    }

    try {
        const response = await fetch(`${API_BASE}/control/update-interval?service=${serviceId}&intervalMs=${ms}`, { method: 'POST' });
        if (!response.ok) throw new Error('Błąd zmiany interwału');

        showAlert(`Interwał dla ${serviceId} zmieniony na ${ms}ms.`, 'success');

        if (document.activeElement) {
            document.activeElement.blur();
        }
        fetchServicesState();
    } catch (error) {
        showAlert(`Nie udało się zmienić interwału dla ${serviceId}.`, 'danger');
    }
}

// Powiadomienia UI
function showAlert(message, type) {
    const alertsContainer = document.getElementById('alertsContainer');
    const alert = document.createElement('div');
    alert.className = `alert alert-${type} alert-dismissible fade show`;
    alert.innerHTML = `
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    `;
    alertsContainer.appendChild(alert);

    setTimeout(() => {
        if (alert.parentNode) alert.parentNode.removeChild(alert);
    }, 4000);
}

// Inicjalizacja i pętla odświeżania
document.addEventListener('DOMContentLoaded', () => {
    fetchServicesState();
    setInterval(fetchServicesState, 3000);
});