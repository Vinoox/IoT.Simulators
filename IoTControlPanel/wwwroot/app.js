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
    const grid = document.getElementById('servicesGrid');
    grid.innerHTML = ''; // Wyczyść obecny stan

    if (services.length === 0) {
        grid.innerHTML = `<div class="alert alert-info">Brak zarejestrowanych symulatorów. Uruchom serwisy, aby pojawiły się w panelu.</div>`;
        return;
    }

    services.forEach(service => {
        const isRunning = service.isRunning;
        const statusBadge = isRunning
            ? `<span class="badge bg-success">W TRAKCIE NADAWANIA</span>`
            : `<span class="badge bg-warning text-dark">ZATRZYMANY</span>`;

        const card = document.createElement('div');
        card.className = 'col-md-6 col-lg-4 mb-4';
        card.innerHTML = `
            <div class="card h-100 shadow-sm border-${isRunning ? 'success' : 'warning'}">
                <div class="card-header d-flex justify-content-between align-items-center">
                    <strong>${service.serviceId.toUpperCase()}</strong>
                    ${statusBadge}
                </div>
                <div class="card-body">
                    <p class="mb-1"><small class="text-muted">Adres bazowy:</small><br> <code>${service.baseUrl}</code></p>
                    <p class="mb-1"><small class="text-muted">Cel (Broker/Endpoint):</small><br> <code>${service.targetAddress}</code></p>
                    <p class="mb-1"><small class="text-muted">Protokół:</small> <strong>${service.protocol}</strong></p>
                    <p class="mb-3"><small class="text-muted">Ścieżka/Temat:</small><br> <span class="badge bg-secondary">${service.topicOrPath}</span></p>
                    
                    <div class="input-group input-group-sm mb-3">
                        <span class="input-group-text">Interwał (ms)</span>
                        <input type="number" class="form-control" id="interval-${service.serviceId}" value="${service.intervalMilliseconds}">
                        <button class="btn btn-outline-primary" onclick="updateInterval('${service.serviceId}')">Zmień</button>
                    </div>

                    <div class="d-grid gap-2 d-md-flex justify-content-md-center">
                        <button class="btn btn-${isRunning ? 'outline-danger' : 'success'} btn-sm w-100" 
                                onclick="toggleState('${service.serviceId}', ${isRunning})">
                            ${isRunning ? '<i class="bi bi-stop-circle"></i> Zatrzymaj' : '<i class="bi bi-play-circle"></i> Wznów'}
                        </button>
                    </div>
                </div>
                <div class="card-footer text-muted text-end" style="font-size: 0.75rem;">
                    Aktualizacja stempla: ${new Date(service.lastUpdated).toLocaleTimeString()}
                </div>
            </div>
        `;
        grid.appendChild(card);
    });
}

// Akcje sterujące
async function toggleState(serviceId, currentlyRunning) {
    const action = currentlyRunning ? 'stop' : 'start';
    try {
        const response = await fetch(`${API_BASE}/control/${action}?service=${serviceId}`, { method: 'POST' });
        if (!response.ok) throw new Error('Błąd wykonania akcji');

        showAlert(`Polecenie ${action.toUpperCase()} wysłane do ${serviceId}.`, 'success');
        fetchServicesState(); // Natychmiastowe odświeżenie UI
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

    // Automatyczne zamykanie alertu po 4 sekundach
    setTimeout(() => {
        if (alert.parentNode) alert.parentNode.removeChild(alert);
    }, 4000);
}

// Inicjalizacja i pętla odświeżania (odpytywanie co 3 sekundy)
document.addEventListener('DOMContentLoaded', () => {
    fetchServicesState();
    setInterval(fetchServicesState, 3000);
});