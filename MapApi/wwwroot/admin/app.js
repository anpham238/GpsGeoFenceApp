(function () {
  const tokenKey = "gps_admin_token";
  const profileKey = "gps_admin_profile";

  const state = {
    token: localStorage.getItem(tokenKey) || "",
    profile: parseJson(localStorage.getItem(profileKey), null),
    pois: [],
    currentPoi: null,
    dashboard: null,
    currentRoute: "dashboard",
    currentRequestId: 0,
    poiFormBusy: false
  };

  const el = {
    loginScreen: byId("loginScreen"),
    appShell: byId("appShell"),
    loginForm: byId("loginForm"),
    loginUsername: byId("loginUsername"),
    loginPassword: byId("loginPassword"),
    loginError: byId("loginError"),
    logoutBtn: byId("logoutBtn"),
    currentUserLabel: byId("currentUserLabel"),
    navLinks: Array.from(document.querySelectorAll(".nav-link")),
    pageTitle: byId("pageTitle"),
    pageSubtitle: byId("pageSubtitle"),
    globalStatus: byId("globalStatus"),
    refreshBtn: byId("refreshBtn"),
    newPoiBtn: byId("newPoiBtn"),
    dashboardView: byId("dashboardView"),
    poisView: byId("poisView"),
    formView: byId("formView"),
    logsView: byId("logsView"),
    dashboardMetrics: byId("dashboardMetrics"),
    dashboardTopPois: byId("dashboardTopPois"),
    dashboardLanguages: byId("dashboardLanguages"),
    poiSearchInput: byId("poiSearchInput"),
    poiFilterActive: byId("poiFilterActive"),
    poiSearchBtn: byId("poiSearchBtn"),
    poiListLoading: byId("poiListLoading"),
    poiTableBody: byId("poiTableBody"),
    poiForm: byId("poiForm"),
    poiIdInput: byId("poiIdInput"),
    poiNameInput: byId("poiNameInput"),
    poiDescriptionInput: byId("poiDescriptionInput"),
    poiNarrationInput: byId("poiNarrationInput"),
    poiLatitudeInput: byId("poiLatitudeInput"),
    poiLongitudeInput: byId("poiLongitudeInput"),
    poiRadiusInput: byId("poiRadiusInput"),
    poiNearRadiusInput: byId("poiNearRadiusInput"),
    poiCooldownInput: byId("poiCooldownInput"),
    poiIsActiveInput: byId("poiIsActiveInput"),
    poiResetBtn: byId("poiResetBtn"),
    languageTagInput: byId("languageTagInput"),
    languageTextInput: byId("languageTextInput"),
    saveLanguageBtn: byId("saveLanguageBtn"),
    languageList: byId("languageList"),
    imageUploadInput: byId("imageUploadInput"),
    audioUploadInput: byId("audioUploadInput"),
    mediaImageUrlInput: byId("mediaImageUrlInput"),
    mediaAudioUrlInput: byId("mediaAudioUrlInput"),
    mediaMapLinkInput: byId("mediaMapLinkInput"),
    saveMediaLinksBtn: byId("saveMediaLinksBtn"),
    mediaPreview: byId("mediaPreview"),
    qrPreviewWrap: byId("qrPreviewWrap"),
    logsPoiSelect: byId("logsPoiSelect"),
    loadLogsBtn: byId("loadLogsBtn"),
    logsSummary: byId("logsSummary"),
    logsTable: byId("logsTable")
  };

  boot();

  async function boot() {
    wireEvents();

    if (!state.token) {
      renderAuth(false);
      return;
    }

    try {
      const profile = await api("/api/v1/auth/me");
      if (!profile || profile.role !== "admin") {
        throw new Error("Tai khoan hien tai khong co role admin.");
      }

      state.profile = profile;
      localStorage.setItem(profileKey, JSON.stringify(profile));
      renderAuth(true);
      await initializeApp();
    } catch (error) {
      clearSession();
      renderAuth(false);
      showLoginError(error.message || "Khong xac thuc duoc tai khoan admin.");
    }
  }

  function wireEvents() {
    el.loginForm.addEventListener("submit", onLoginSubmit);
    el.logoutBtn.addEventListener("click", onLogout);
    el.refreshBtn.addEventListener("click", () => loadCurrentRoute());
    el.newPoiBtn.addEventListener("click", () => openPoiForm());
    el.poiSearchBtn.addEventListener("click", () => loadPoiList());
    el.poiForm.addEventListener("submit", onSavePoi);
    el.poiResetBtn.addEventListener("click", () => openPoiForm());
    el.saveLanguageBtn.addEventListener("click", onSaveLanguage);
    el.saveMediaLinksBtn.addEventListener("click", onSaveMediaLinks);
    el.imageUploadInput.addEventListener("change", onUploadImage);
    el.audioUploadInput.addEventListener("change", onUploadAudio);
    el.loadLogsBtn.addEventListener("click", () => loadLogs());

    el.navLinks.forEach((button) => {
      button.addEventListener("click", () => navigate(button.dataset.route));
    });
  }

  async function onLoginSubmit(event) {
    event.preventDefault();
    showLoginError("");

    const username = el.loginUsername.value.trim();
    const password = el.loginPassword.value;

    if (!username || !password) {
      showLoginError("Nhap day du ten dang nhap va mat khau.");
      return;
    }

    try {
      el.loginForm.querySelector("button[type='submit']").disabled = true;
      const result = await api("/api/v1/auth/login", {
        method: "POST",
        body: JSON.stringify({ username, password })
      }, false);

      if (!result || !result.token) {
        throw new Error("Dang nhap that bai.");
      }

      state.token = result.token;
      localStorage.setItem(tokenKey, state.token);

      const profile = await api("/api/v1/auth/me");
      if (!profile || profile.role !== "admin") {
        throw new Error("Tai khoan nay khong co role admin.");
      }

      state.profile = profile;
      localStorage.setItem(profileKey, JSON.stringify(profile));
      renderAuth(true);
      await initializeApp();
    } catch (error) {
      clearSession();
      renderAuth(false);
      showLoginError(error.message || "Dang nhap that bai.");
    } finally {
      el.loginForm.querySelector("button[type='submit']").disabled = false;
    }
  }

  async function initializeApp() {
    el.currentUserLabel.textContent = state.profile
      ? `${state.profile.username} • ${state.profile.role}`
      : "";

    await loadPoiCatalog();
    navigate("dashboard");
  }

  function navigate(route) {
    state.currentRoute = route;

    el.navLinks.forEach((button) => {
      button.classList.toggle("active", button.dataset.route === route);
    });

    [el.dashboardView, el.poisView, el.formView, el.logsView].forEach((view) => view.classList.add("hidden"));

    if (route === "dashboard") {
      el.pageTitle.textContent = "Dashboard";
      el.pageSubtitle.textContent = "Tong quan noi dung va su dung he thong.";
      el.dashboardView.classList.remove("hidden");
      loadDashboard();
      return;
    }

    if (route === "pois") {
      el.pageTitle.textContent = "POI List";
      el.pageSubtitle.textContent = "Tim, loc va dieu huong den form chi tiet.";
      el.poisView.classList.remove("hidden");
      loadPoiList();
      return;
    }

    if (route === "form") {
      el.pageTitle.textContent = "POI Form";
      el.pageSubtitle.textContent = "Tao, sua, quan ly media va narration da ngon ngu.";
      el.formView.classList.remove("hidden");
      if (!state.currentPoi) {
        renderPoiForm();
      } else {
        renderPoiForm();
      }
      return;
    }

    if (route === "logs") {
      el.pageTitle.textContent = "Logs";
      el.pageSubtitle.textContent = "Thong ke co ban theo POI de demo kha nang van hanh.";
      el.logsView.classList.remove("hidden");
      hydrateLogsSelect();
      loadLogs();
    }
  }

  async function loadCurrentRoute() {
    if (state.currentRoute === "dashboard") return loadDashboard();
    if (state.currentRoute === "pois") return loadPoiList();
    if (state.currentRoute === "form") {
      if (state.currentPoi && state.currentPoi.id) {
        await loadPoiDetail(state.currentPoi.id);
      } else {
        renderPoiForm();
      }
      return;
    }

    if (state.currentRoute === "logs") return loadLogs();
  }

  async function loadDashboard() {
    setGlobalStatus("Dang tai dashboard...", false);
    try {
      const [dashboard, seedStatus] = await Promise.all([
        api("/api/v1/admin/dashboard"),
        api("/api/v1/admin/seed/status")
      ]);

      state.dashboard = dashboard;
      renderDashboard(dashboard, seedStatus);
      clearGlobalStatus();
    } catch (error) {
      setGlobalStatus(error.message || "Khong tai duoc dashboard.", true);
    }
  }

  function renderDashboard(dashboard, seedStatus) {
    const metrics = [
      ["Tong POI", dashboard.totalPois],
      ["Dang active", dashboard.activePois],
      ["Dang inactive", dashboard.inactivePois],
      ["Tong translation", dashboard.totalTranslations],
      ["Tong kich hoat", dashboard.totalActivations],
      ["Tong giay nghe", dashboard.totalListeningSeconds]
    ];

    el.dashboardMetrics.innerHTML = metrics.map(([label, value]) => `
      <article class="metric">
        <div class="muted">${label}</div>
        <div class="metric-value">${value ?? 0}</div>
      </article>
    `).join("");

    const topPois = dashboard.topPois || [];
    el.dashboardTopPois.innerHTML = topPois.length
      ? topPois.map((item) => `
          <div class="list-item">
            <strong>${escapeHtml(item.poiName)}</strong>
            <div class="muted small">POI #${item.poiId}</div>
            <div class="small">Kich hoat: <strong>${item.activations}</strong></div>
            <div class="small">Giay nghe: <strong>${item.listeningSeconds}</strong></div>
          </div>
        `).join("")
      : `<div class="muted">Chua co du lieu logs.</div>`;

    const byLanguage = seedStatus.byLanguage || [];
    el.dashboardLanguages.innerHTML = byLanguage.length
      ? byLanguage.map((item) => `
          <div class="list-item">
            <strong>${escapeHtml(item.language)}</strong>
            <div class="small">So ban dich: ${item.count}</div>
          </div>
        `).join("")
      : `<div class="muted">Chua co du lieu ngon ngu.</div>`;
  }

  async function loadPoiCatalog() {
    const data = await api("/api/v1/pois/search?page=1&pageSize=200");
    state.pois = data.items || [];
    hydrateLogsSelect();
  }

  async function loadPoiList() {
    const requestId = nextRequestId();
    el.poiListLoading.classList.remove("hidden");
    setGlobalStatus("Dang tai danh sach POI...", false);

    const params = new URLSearchParams({
      page: "1",
      pageSize: "200"
    });

    const search = el.poiSearchInput.value.trim();
    const isActive = el.poiFilterActive.value;
    if (search) params.set("search", search);
    if (isActive) params.set("isActive", isActive);

    try {
      const data = await api(`/api/v1/pois/search?${params.toString()}`);
      if (!isLatestRequest(requestId)) return;
      state.pois = data.items || [];
      renderPoiTable();
      hydrateLogsSelect();
      clearGlobalStatus();
    } catch (error) {
      setGlobalStatus(error.message || "Khong tai duoc danh sach POI.", true);
    } finally {
      el.poiListLoading.classList.add("hidden");
    }
  }

  function renderPoiTable() {
    el.poiTableBody.innerHTML = state.pois.map((poi) => `
      <tr>
        <td class="mono">${poi.id}</td>
        <td>
          <strong>${escapeHtml(poi.name)}</strong>
          <div class="muted small">${escapeHtml(poi.description || "")}</div>
        </td>
        <td class="small">${poi.latitude.toFixed(5)}, ${poi.longitude.toFixed(5)}</td>
        <td>
          <span class="badge ${poi.isActive ? "" : "inactive"}">${poi.isActive ? "Active" : "Inactive"}</span>
        </td>
        <td class="small">
          ${poi.imageUrl ? "Anh" : "-"} / ${poi.audioUrl ? "Audio" : "-"}
        </td>
        <td class="small">${escapeHtml(poi.language || "vi-VN")}</td>
        <td>
          <div class="actions">
            <button class="btn subtle" data-action="edit" data-id="${poi.id}">Sua</button>
            <button class="btn subtle" data-action="toggle" data-id="${poi.id}">${poi.isActive ? "Tat" : "Bat"}</button>
            <button class="btn danger" data-action="delete" data-id="${poi.id}">Xoa</button>
          </div>
        </td>
      </tr>
    `).join("");

    Array.from(el.poiTableBody.querySelectorAll("button")).forEach((button) => {
      button.addEventListener("click", async () => {
        const id = Number(button.dataset.id);
        const action = button.dataset.action;

        if (action === "edit") {
          await loadPoiDetail(id);
          navigate("form");
          return;
        }

        if (action === "toggle") {
          await togglePoiStatus(id);
          return;
        }

        if (action === "delete") {
          await deletePoi(id);
        }
      });
    });
  }

  async function loadPoiDetail(id) {
    const requestId = nextRequestId();
    setGlobalStatus(`Dang tai chi tiet POI #${id}...`, false);
    try {
      const detail = await api(`/api/v1/pois/${id}?lang=vi-VN`);
      if (!isLatestRequest(requestId)) return null;
      state.currentPoi = detail;
      renderPoiForm();
      clearGlobalStatus();
      return detail;
    } catch (error) {
      setGlobalStatus(error.message || "Khong tai duoc chi tiet POI.", true);
      throw error;
    }
  }

  function openPoiForm() {
    state.currentPoi = null;
    renderPoiForm();
    if (state.currentRoute !== "form") {
      navigate("form");
    }
  }

  function renderPoiForm() {
    const poi = state.currentPoi;
    el.poiIdInput.value = poi?.id || "";
    el.poiNameInput.value = poi?.name || "";
    el.poiDescriptionInput.value = poi?.description || "";
    el.poiNarrationInput.value = findLanguageText(poi, "vi-VN");
    el.poiLatitudeInput.value = poi?.latitude ?? "";
    el.poiLongitudeInput.value = poi?.longitude ?? "";
    el.poiRadiusInput.value = poi?.radiusMeters ?? 120;
    el.poiNearRadiusInput.value = poi?.nearRadiusMeters ?? 240;
    el.poiCooldownInput.value = poi?.cooldownSeconds ?? 30;
    el.poiIsActiveInput.checked = poi?.isActive ?? true;

    el.languageTagInput.value = "vi-VN";
    el.languageTextInput.value = "";
    el.mediaImageUrlInput.value = poi?.media?.imageUrl || poi?.imageUrl || "";
    el.mediaAudioUrlInput.value = poi?.media?.audioUrl || poi?.audioUrl || "";
    el.mediaMapLinkInput.value = poi?.media?.mapLink || poi?.mapLink || "";

    renderLanguageList();
    renderMediaPreview();
    renderQrPreview();
  }

  function renderLanguageList() {
    const languages = state.currentPoi?.languages || [];
    el.languageList.innerHTML = languages.length
      ? languages.map((item) => `
          <div class="list-item">
            <div class="actions">
              <strong>${escapeHtml(item.languageTag)}</strong>
              <button class="btn subtle" data-action="load-language" data-lang="${item.languageTag}">Nap vao form</button>
              <button class="btn subtle" data-action="delete-language" data-lang="${item.languageTag}">Xoa</button>
            </div>
            <div class="muted small">${escapeHtml(item.textToSpeech || "(khong co noi dung)")}</div>
          </div>
        `).join("")
      : `<div class="muted">Chua co ngon ngu nao.</div>`;

    Array.from(el.languageList.querySelectorAll("button")).forEach((button) => {
      const lang = button.dataset.lang;
      const action = button.dataset.action;
      button.addEventListener("click", async () => {
        if (action === "load-language") {
          const item = languages.find((x) => x.languageTag === lang);
          el.languageTagInput.value = lang;
          el.languageTextInput.value = item?.textToSpeech || "";
          return;
        }

        if (action === "delete-language") {
          await deleteLanguage(lang);
        }
      });
    });
  }

  function renderMediaPreview() {
    const poi = state.currentPoi;
    const imageUrl = poi?.media?.imageUrl || poi?.imageUrl;
    const audioUrl = poi?.media?.audioUrl || poi?.audioUrl;
    const mapLink = poi?.media?.mapLink || poi?.mapLink;

    el.mediaPreview.innerHTML = `
      ${imageUrl ? `<img src="${imageUrl}" alt="POI image" />` : `<div class="muted">Chua co anh.</div>`}
      ${audioUrl ? `<audio controls src="${audioUrl}"></audio>` : `<div class="muted">Chua co audio.</div>`}
      ${mapLink ? `<a href="${mapLink}" target="_blank" rel="noreferrer">Mo map link</a>` : `<div class="muted">Chua co map link.</div>`}
    `;
  }

  function renderQrPreview() {
    const id = state.currentPoi?.id;
    if (!id) {
      el.qrPreviewWrap.className = "qr-wrap muted";
      el.qrPreviewWrap.innerHTML = "Luu POI de xem QR.";
      return;
    }

    el.qrPreviewWrap.className = "qr-wrap";
    el.qrPreviewWrap.innerHTML = `
      <div>
        <img src="/api/v1/qr/generate/${id}" alt="QR ${id}" />
        <div class="muted small">Payload: smarttourism://poi/${id}</div>
      </div>
    `;
  }

  async function onSavePoi(event) {
    event.preventDefault();
    if (state.poiFormBusy) return;

    const payload = {
      name: el.poiNameInput.value.trim(),
      description: el.poiDescriptionInput.value.trim() || null,
      narrationText: el.poiNarrationInput.value.trim() || null,
      latitude: Number(el.poiLatitudeInput.value),
      longitude: Number(el.poiLongitudeInput.value),
      radiusMeters: Number(el.poiRadiusInput.value || 120),
      nearRadiusMeters: Number(el.poiNearRadiusInput.value || 240),
      cooldownSeconds: Number(el.poiCooldownInput.value || 30),
      debounceSeconds: 3,
      priority: null,
      isActive: !!el.poiIsActiveInput.checked
    };

    const validationMessage = validatePoiPayload(payload);
    if (validationMessage) {
      setGlobalStatus(validationMessage, true);
      return;
    }

    const id = el.poiIdInput.value;
    const isUpdate = Boolean(id);

    setGlobalStatus(isUpdate ? "Dang cap nhat POI..." : "Dang tao POI...", false);
    try {
      setPoiFormBusy(true);
      const saved = await api(isUpdate ? `/api/v1/pois/${id}` : "/api/v1/pois", {
        method: isUpdate ? "PUT" : "POST",
        body: JSON.stringify(payload)
      });

      state.currentPoi = saved;
      await loadPoiCatalog();
      renderPoiForm();
      setGlobalStatus(`Da luu POI #${saved.id}.`, false);
    } catch (error) {
      setGlobalStatus(error.message || "Khong luu duoc POI.", true);
    } finally {
      setPoiFormBusy(false);
    }
  }

  async function onSaveLanguage() {
    if (!ensureCurrentPoiId()) return;
    if (state.poiFormBusy) return;

    const languageTag = el.languageTagInput.value.trim();
    if (!languageTag) {
      setGlobalStatus("Can nhap language tag.", true);
      return;
    }

    if (!el.languageTextInput.value.trim()) {
      setGlobalStatus("Can nhap noi dung narration/toi thieu cho ngon ngu nay.", true);
      return;
    }

    setGlobalStatus(`Dang luu ngon ngu ${languageTag}...`, false);
    try {
      setPoiFormBusy(true);
      await api(`/api/v1/pois/${state.currentPoi.id}/languages/${encodeURIComponent(languageTag)}`, {
        method: "PUT",
        body: JSON.stringify({
          languageTag,
          textToSpeech: el.languageTextInput.value.trim() || null
        })
      });

      await loadPoiDetail(state.currentPoi.id);
      setGlobalStatus(`Da luu ngon ngu ${languageTag}.`, false);
    } catch (error) {
      setGlobalStatus(error.message || "Khong luu duoc ngon ngu.", true);
    } finally {
      setPoiFormBusy(false);
    }
  }

  async function deleteLanguage(languageTag) {
    if (!ensureCurrentPoiId()) return;
    if (state.poiFormBusy) return;
    if (!confirm(`Xoa ngon ngu ${languageTag}?`)) return;

    try {
      setPoiFormBusy(true);
      await api(`/api/v1/pois/${state.currentPoi.id}/languages/${encodeURIComponent(languageTag)}`, {
        method: "DELETE"
      });
      await loadPoiDetail(state.currentPoi.id);
      setGlobalStatus(`Da xoa ngon ngu ${languageTag}.`, false);
    } catch (error) {
      setGlobalStatus(error.message || "Khong xoa duoc ngon ngu.", true);
    } finally {
      setPoiFormBusy(false);
    }
  }

  async function onSaveMediaLinks() {
    if (!ensureCurrentPoiId()) return;
    if (state.poiFormBusy) return;

    const mediaValidationError = validateMediaLinks({
      imageUrl: el.mediaImageUrlInput.value.trim(),
      audioUrl: el.mediaAudioUrlInput.value.trim(),
      mapLink: el.mediaMapLinkInput.value.trim()
    });
    if (mediaValidationError) {
      setGlobalStatus(mediaValidationError, true);
      return;
    }

    setGlobalStatus("Dang luu media links...", false);
    try {
      setPoiFormBusy(true);
      await api(`/api/v1/pois/${state.currentPoi.id}/media/links`, {
        method: "PUT",
        body: JSON.stringify({
          imageUrl: el.mediaImageUrlInput.value.trim() || null,
          audioUrl: el.mediaAudioUrlInput.value.trim() || null,
          mapLink: el.mediaMapLinkInput.value.trim() || null
        })
      });

      await loadPoiDetail(state.currentPoi.id);
      setGlobalStatus("Da luu media links.", false);
    } catch (error) {
      setGlobalStatus(error.message || "Khong luu duoc media links.", true);
    } finally {
      setPoiFormBusy(false);
    }
  }

  async function onUploadImage(event) {
    if (!ensureCurrentPoiId()) return;
    if (state.poiFormBusy) return;
    const file = event.target.files[0];
    if (!file) return;

    if (!file.type.startsWith("image/")) {
      setGlobalStatus("File anh khong hop le.", true);
      event.target.value = "";
      return;
    }

    const formData = new FormData();
    formData.append("file", file);
    await uploadMedia(`/api/v1/pois/${state.currentPoi.id}/media/image`, formData, "anh");
    event.target.value = "";
  }

  async function onUploadAudio(event) {
    if (!ensureCurrentPoiId()) return;
    if (state.poiFormBusy) return;
    const file = event.target.files[0];
    if (!file) return;

    if (!file.type.startsWith("audio/")) {
      setGlobalStatus("File audio khong hop le.", true);
      event.target.value = "";
      return;
    }

    const formData = new FormData();
    formData.append("file", file);
    await uploadMedia(`/api/v1/pois/${state.currentPoi.id}/media/audio`, formData, "audio");
    event.target.value = "";
  }

  async function uploadMedia(url, formData, label) {
    setGlobalStatus(`Dang upload ${label}...`, false);
    try {
      setPoiFormBusy(true);
      await api(url, {
        method: "POST",
        body: formData,
        isFormData: true
      });
      await loadPoiDetail(state.currentPoi.id);
      setGlobalStatus(`Da upload ${label}.`, false);
    } catch (error) {
      setGlobalStatus(error.message || `Khong upload duoc ${label}.`, true);
    } finally {
      setPoiFormBusy(false);
    }
  }

  async function togglePoiStatus(id) {
    const poi = state.pois.find((x) => x.id === id);
    if (!poi) return;

    try {
      disablePoiRowActions(id, true);
      await api(`/api/v1/pois/${id}/status`, {
        method: "PATCH",
        body: JSON.stringify({ isActive: !poi.isActive })
      });
      await loadPoiList();
      if (state.currentPoi?.id === id) {
        await loadPoiDetail(id);
      }
      setGlobalStatus(`Da cap nhat trang thai POI #${id}.`, false);
    } catch (error) {
      setGlobalStatus(error.message || "Khong doi duoc trang thai.", true);
    } finally {
      disablePoiRowActions(id, false);
    }
  }

  async function deletePoi(id) {
    if (state.poiFormBusy) return;
    if (!confirm(`Xoa POI #${id}?`)) return;

    try {
      disablePoiRowActions(id, true);
      await api(`/api/v1/pois/${id}`, { method: "DELETE" });
      if (state.currentPoi?.id === id) {
        state.currentPoi = null;
        renderPoiForm();
      }
      await loadPoiList();
      setGlobalStatus(`Da xoa POI #${id}.`, false);
    } catch (error) {
      setGlobalStatus(error.message || "Khong xoa duoc POI.", true);
    } finally {
      disablePoiRowActions(id, false);
    }
  }

  function hydrateLogsSelect() {
    const selected = el.logsPoiSelect.value;
    el.logsPoiSelect.innerHTML = state.pois.length
      ? state.pois.map((poi) => `<option value="${poi.id}">${poi.name} (#${poi.id})</option>`).join("")
      : `<option value="">Chua co POI</option>`;

    if (selected) el.logsPoiSelect.value = selected;
    if (!el.logsPoiSelect.value && state.pois[0]) el.logsPoiSelect.value = String(state.pois[0].id);
  }

  async function loadLogs() {
    const poiId = Number(el.logsPoiSelect.value);
    if (!poiId) {
      el.logsSummary.innerHTML = `<div class="muted">Chua co POI de xem logs.</div>`;
      el.logsTable.innerHTML = "";
      return;
    }

    setGlobalStatus(`Dang tai logs cho POI #${poiId}...`, false);
    try {
      const rows = await api(`/api/v1/history/pois/${poiId}`);
      renderLogs(rows || []);
      clearGlobalStatus();
    } catch (error) {
      setGlobalStatus(error.message || "Khong tai duoc logs.", true);
    }
  }

  function renderLogs(rows) {
    const totalActivations = rows.reduce((sum, item) => sum + (item.quantity || 0), 0);
    const totalListeningSeconds = rows.reduce((sum, item) => sum + (item.totalDurationSeconds || 0), 0);

    el.logsSummary.innerHTML = `
      <div class="metric">
        <div class="muted">So lan kich hoat</div>
        <div class="metric-value">${totalActivations}</div>
      </div>
      <div class="metric">
        <div class="muted">Tong giay nghe</div>
        <div class="metric-value">${totalListeningSeconds}</div>
      </div>
      <div class="metric">
        <div class="muted">So dong log</div>
        <div class="metric-value">${rows.length}</div>
      </div>
    `;

    el.logsTable.innerHTML = rows.length
      ? `<div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>POI</th>
                <th>Quantity</th>
                <th>Last Visited</th>
                <th>Total Duration</th>
              </tr>
            </thead>
            <tbody>
              ${rows.map((item) => `
                <tr>
                  <td>${escapeHtml(item.poiName)}</td>
                  <td>${item.quantity}</td>
                  <td>${formatDate(item.lastVisitedAt)}</td>
                  <td>${item.totalDurationSeconds || 0}s</td>
                </tr>
              `).join("")}
            </tbody>
          </table>
        </div>`
      : `<div class="muted">Chua co logs cho POI nay.</div>`;
  }

  function renderAuth(isAuthenticated) {
    el.loginScreen.classList.toggle("hidden", isAuthenticated);
    el.appShell.classList.toggle("hidden", !isAuthenticated);
  }

  function showLoginError(message) {
    el.loginError.textContent = message || "";
  }

  function setGlobalStatus(message, isError) {
    el.globalStatus.textContent = message || "";
    el.globalStatus.classList.remove("hidden");
    el.globalStatus.classList.toggle("error", !!isError);
  }

  function clearGlobalStatus() {
    el.globalStatus.classList.add("hidden");
    el.globalStatus.classList.remove("error");
    el.globalStatus.textContent = "";
  }

  function ensureCurrentPoiId() {
    if (!state.currentPoi || !state.currentPoi.id) {
      setGlobalStatus("Hay luu POI truoc khi thao tac media hoac narration.", true);
      return false;
    }

    return true;
  }

  function validatePoiPayload(payload) {
    if (!payload.name) return "Ten POI khong duoc de trong.";
    if (payload.name.length > 200) return "Ten POI qua dai.";
    if (!Number.isFinite(payload.latitude) || payload.latitude < -90 || payload.latitude > 90) {
      return "Latitude khong hop le.";
    }
    if (!Number.isFinite(payload.longitude) || payload.longitude < -180 || payload.longitude > 180) {
      return "Longitude khong hop le.";
    }
    if (!Number.isFinite(payload.radiusMeters) || payload.radiusMeters < 30) {
      return "Ban kinh kich hoat phai tu 30m tro len.";
    }
    if (!Number.isFinite(payload.nearRadiusMeters) || payload.nearRadiusMeters < payload.radiusMeters) {
      return "Near radius phai lon hon hoac bang radius.";
    }
    if (!Number.isFinite(payload.cooldownSeconds) || payload.cooldownSeconds < 0) {
      return "Cooldown khong hop le.";
    }

    return "";
  }

  function validateMediaLinks(links) {
    if (links.imageUrl && !isHttpUrl(links.imageUrl)) return "Image URL khong hop le.";
    if (links.audioUrl && !isHttpUrl(links.audioUrl)) return "Audio URL khong hop le.";
    if (links.mapLink && !isHttpUrl(links.mapLink)) return "Map link khong hop le.";
    return "";
  }

  function setPoiFormBusy(isBusy) {
    state.poiFormBusy = isBusy;
    Array.from(el.poiForm.querySelectorAll("button, input, textarea")).forEach((element) => {
      if (element.id === "poiResetBtn") return;
      element.disabled = isBusy;
    });
    el.saveLanguageBtn.disabled = isBusy;
    el.saveMediaLinksBtn.disabled = isBusy;
    el.imageUploadInput.disabled = isBusy;
    el.audioUploadInput.disabled = isBusy;
  }

  function onLogout() {
    clearSession();
    renderAuth(false);
    showLoginError("");
  }

  function clearSession() {
    state.token = "";
    state.profile = null;
    localStorage.removeItem(tokenKey);
    localStorage.removeItem(profileKey);
  }

  function disablePoiRowActions(id, isDisabled) {
    Array.from(el.poiTableBody.querySelectorAll(`button[data-id='${id}']`)).forEach((button) => {
      button.disabled = isDisabled;
    });
  }

  function nextRequestId() {
    state.currentRequestId += 1;
    return state.currentRequestId;
  }

  function isLatestRequest(requestId) {
    return requestId === state.currentRequestId;
  }

  function isHttpUrl(value) {
    try {
      const url = new URL(value);
      return url.protocol === "http:" || url.protocol === "https:";
    } catch {
      return false;
    }
  }

  async function api(url, options = {}, includeAuth = true) {
    const headers = new Headers(options.headers || {});
    if (!options.isFormData && !headers.has("Content-Type")) {
      headers.set("Content-Type", "application/json");
    }

    if (includeAuth && state.token) {
      headers.set("Authorization", `Bearer ${state.token}`);
    }

    const response = await fetch(url, {
      method: options.method || "GET",
      headers,
      body: options.body
    });

    if (!response.ok) {
      let message = `Request failed (${response.status})`;
      try {
        const contentType = response.headers.get("content-type") || "";
        if (contentType.includes("application/json")) {
          const payload = await response.json();
          message = payload.message || payload.error || message;
        } else {
          const text = await response.text();
          if (text) message = text;
        }
      } catch (error) {
      }

      throw new Error(message);
    }

    const contentType = response.headers.get("content-type") || "";
    if (contentType.includes("application/json")) {
      return response.json();
    }

    return response.text();
  }

  function findLanguageText(poi, languageTag) {
    return poi?.languages?.find((item) => item.languageTag === languageTag)?.textToSpeech || "";
  }

  function formatDate(value) {
    if (!value) return "-";
    const date = new Date(value);
    return date.toLocaleString("vi-VN");
  }

  function parseJson(value, fallback) {
    try {
      return value ? JSON.parse(value) : fallback;
    } catch {
      return fallback;
    }
  }

  function escapeHtml(value) {
    return String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll("\"", "&quot;")
      .replaceAll("'", "&#39;");
  }

  function byId(id) {
    return document.getElementById(id);
  }
})();
