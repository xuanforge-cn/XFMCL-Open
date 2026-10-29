/**
 * LauncherScreen.kt - 主界面
 * 玹砺科技 XUANFORGE
 *
 * 单页布局：离线登录、下载源、版本列表、进度与日志。
 * 本项目以 MIT 协议开源。
 */

package cn.xuanforge.xfmclopen.ui

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.systemBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilterChipDefaults
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cn.xuanforge.xfmclopen.data.DownloadSource
import cn.xuanforge.xfmclopen.data.OfflineIdentity
import cn.xuanforge.xfmclopen.data.VersionRow

private val TextHi = Color(0xFFEEF2F9)
private val TextMid = Color(0xFF9AA4B8)
private val TextLow = Color(0xFF5F6879)
private val Accent = Color(0xFF2F6BFF)

@Composable
fun LauncherScreen() {
    val context = LocalContext.current
    val state = remember { LauncherState(context.applicationContext) }
    LaunchedEffect(Unit) { state.start() }

    val logScroll = rememberScrollState()
    LaunchedEffect(state.log) { logScroll.scrollTo(logScroll.maxValue) }

    Surface(modifier = Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.background) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .background(Brush.verticalGradient(listOf(Color(0xFF0B0F18), Color(0xFF080A0F))))
                .systemBarsPadding()
                .padding(horizontal = 14.dp, vertical = 10.dp)
        ) {
            Header()
            Spacer(Modifier.height(10.dp))
            LoginCard(state)
            Spacer(Modifier.height(8.dp))
            SearchRow(state)
            Spacer(Modifier.height(8.dp))
            VersionList(state)
            Spacer(Modifier.height(8.dp))
            StatusBlock(state)
            Spacer(Modifier.height(8.dp))
            ActionRow(state)
            Spacer(Modifier.height(8.dp))
            LogBlock(state, logScroll)
            Spacer(Modifier.height(4.dp))
            Text(
                text = "玹砺科技 XUANFORGE · MIT License",
                fontSize = 9.sp,
                color = TextLow
            )
        }
    }
}

@Composable
private fun Header() {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Box(
            modifier = Modifier
                .size(36.dp)
                .clip(RoundedCornerShape(10.dp))
                .background(Brush.linearGradient(listOf(Color(0xFF4C86FF), Color(0xFF1B58E8)))),
            contentAlignment = Alignment.Center
        ) {
            Text("XO", color = Color.White, fontSize = 12.sp, fontWeight = FontWeight.Bold)
        }

        Spacer(Modifier.width(10.dp))

        Column {
            Text("XFMCL Open", fontSize = 18.sp, fontWeight = FontWeight.SemiBold, color = TextHi)
            Text("跨平台 · 开源 · 仅离线登录", fontSize = 10.sp, color = TextMid)
        }
    }
}

@Composable
private fun LoginCard(state: LauncherState) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = Color(0xFF11151E)),
        border = BorderStroke(1.dp, Color(0xFF242C3B))
    ) {
        Column(modifier = Modifier.padding(12.dp)) {
            Text(
                text = "ACCOUNT",
                fontSize = 9.sp,
                letterSpacing = 1.4.sp,
                fontWeight = FontWeight.SemiBold,
                color = TextLow
            )
            Spacer(Modifier.height(4.dp))
            Text("离线登录", fontSize = 13.sp, fontWeight = FontWeight.SemiBold, color = TextHi)
            Spacer(Modifier.height(8.dp))

            OutlinedTextField(
                value = state.playerName,
                onValueChange = { if (it.length <= 16) state.playerName = it },
                label = { Text("离线玩家名") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth()
            )

            Spacer(Modifier.height(8.dp))

            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                DownloadSource.entries.forEach { item ->
                    FilterChip(
                        selected = state.source == item,
                        onClick = {
                            state.source = item
                            state.saveSettings()
                        },
                        label = { Text(item.label, fontSize = 11.sp) },
                        colors = FilterChipDefaults.filterChipColors(
                            containerColor = Color(0xFF0F141D),
                            labelColor = TextMid,
                            selectedContainerColor = Color(0xFF1E2A44),
                            selectedLabelColor = Color(0xFFDCE7FF)
                        )
                    )
                }
            }

            Spacer(Modifier.height(6.dp))

            Text(
                text = "目录：" + state.paths.root.absolutePath,
                fontSize = 10.sp,
                color = TextLow,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
            Text(
                text = "离线 UUID：" + OfflineIdentity.uuid(state.playerName.ifBlank { "Player" }),
                fontSize = 10.sp,
                color = TextLow,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
        }
    }
}

@Composable
private fun SearchRow(state: LauncherState) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        OutlinedTextField(
            value = state.filter,
            onValueChange = { state.filter = it },
            placeholder = { Text("搜索版本，例如 1.20.1") },
            singleLine = true,
            modifier = Modifier.weight(1f)
        )

        TextButton(onClick = { state.refresh() }, enabled = !state.busy) {
            Text("刷新", fontSize = 12.sp)
        }
    }
}

@Composable
private fun ColumnScope.VersionList(state: LauncherState) {
    val list = state.filteredVersions
    val listState = rememberLazyListState()

    // 选中版本变化或清单刷新后，把选中项滚到可视区域
    LaunchedEffect(state.selectedId, list.size) {
        val index = list.indexOfFirst { it.id == state.selectedId }
        if (index >= 0) {
            listState.scrollToItem(index)
        }
    }

    if (list.isEmpty()) {
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .weight(1f),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = if (state.indeterminate) "正在加载版本清单…" else "没有匹配的版本",
                fontSize = 12.sp,
                color = TextMid
            )
        }
        return
    }

    LazyColumn(
        state = listState,
        modifier = Modifier
            .fillMaxWidth()
            .weight(1f),
        verticalArrangement = Arrangement.spacedBy(5.dp)
    ) {
        items(list, key = { it.id }) { row ->
            VersionItem(row = row, selected = row.id == state.selectedId) {
                state.selectedId = row.id
            }
        }
    }
}

@Composable
private fun VersionItem(row: VersionRow, selected: Boolean, onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(10.dp))
            .background(if (selected) Color(0xFF17233B) else Color(0xFF0F141D))
            .border(
                width = 1.dp,
                color = if (selected) Color(0xFF2C4270) else Color(0xFF1A2130),
                shape = RoundedCornerShape(10.dp)
            )
            .clickable(onClick = onClick)
            .padding(end = 12.dp, top = 9.dp, bottom = 9.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier
                .width(3.dp)
                .height(28.dp)
                .background(
                    if (selected) Accent else Color.Transparent,
                    RoundedCornerShape(2.dp)
                )
        )

        Column(modifier = Modifier.weight(1f).padding(start = 11.dp)) {
            Text(row.id, fontSize = 14.sp, fontWeight = FontWeight.SemiBold, color = if (selected) Color.White else TextHi)
            Text("${row.typeLabel} · ${row.date}", fontSize = 10.sp, color = TextMid)
        }

        if (row.installed) {
            Text(
                text = "已安装",
                fontSize = 10.sp,
                color = Color(0xFF4ADE80),
                modifier = Modifier
                    .clip(RoundedCornerShape(6.dp))
                    .background(Color(0xFF12301F))
                    .border(1.dp, Color(0xFF1E4A31), RoundedCornerShape(6.dp))
                    .padding(horizontal = 8.dp, vertical = 3.dp)
            )
        }
    }
}

@Composable
private fun StatusBlock(state: LauncherState) {
    Column(modifier = Modifier.fillMaxWidth()) {
        Text(state.status, fontSize = 12.sp, color = TextMid)
        Spacer(Modifier.height(4.dp))

        if (state.indeterminate) {
            LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
        } else {
            LinearProgressIndicator(progress = { state.progress }, modifier = Modifier.fillMaxWidth())
        }

        if (state.progressText.isNotEmpty()) {
            Spacer(Modifier.height(4.dp))
            Text(state.progressText, fontSize = 10.sp, color = TextLow)
        }
    }
}

@Composable
private fun ActionRow(state: LauncherState) {
    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        Button(
            onClick = { state.download() },
            enabled = !state.busy && state.selectedVersion != null,
            modifier = Modifier.weight(1f)
        ) {
            Text(if (state.busy) "正在处理…" else "下载 / 补全文件", fontSize = 13.sp)
        }

        if (state.busy) {
            OutlinedButton(onClick = { state.cancel() }) {
                Text("取消", fontSize = 13.sp)
            }
        }
    }
}

@Composable
private fun LogBlock(state: LauncherState, scrollState: androidx.compose.foundation.ScrollState) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .height(104.dp)
            .clip(RoundedCornerShape(10.dp))
            .background(Color(0xFF0B0E15))
            .border(1.dp, Color(0xFF1C2331), RoundedCornerShape(10.dp))
            .padding(9.dp)
    ) {
        Text(
            text = "RUN LOG",
            fontSize = 8.sp,
            letterSpacing = 1.2.sp,
            fontWeight = FontWeight.SemiBold,
            color = TextLow
        )
        Spacer(Modifier.height(4.dp))
        Text(
            text = state.log,
            fontSize = 9.sp,
            fontFamily = FontFamily.Monospace,
            color = Color(0xFF7E8AA2),
            modifier = Modifier.weight(1f).verticalScroll(scrollState)
        )
    }
}
